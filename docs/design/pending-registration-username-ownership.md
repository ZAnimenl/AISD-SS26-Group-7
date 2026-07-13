# Pending Registration Username Ownership

## Problem Definition

Starting email registration keeps a short-lived verification challenge in
memory. The current start endpoint treats the username carried by that pending
challenge as already owned by the unverified email address. Closing the page
does not notify the backend, so an abandoned challenge can make the username
appear taken even though no account exists.

## Option Comparison

- Release the username through a browser-unload or cancellation request:
  rejected because browser exit delivery is unreliable and a cancellation
  endpoint would add an unauthenticated invalidation surface.
- Reserve pending usernames until their verification codes expire: rejected
  because it preserves the reported false conflict and lets an unverified
  caller temporarily deny a username to other users.
- Claim usernames only when registration completes: selected. Pending
  challenges retain the requested username, while the persisted user table
  remains authoritative for ownership. Completion rechecks availability before
  creating the account.

## State Machine

- States: `available`, `pending_verification`, `active_account`, `conflict`, and
  `expired`.
- Events: registration starts, registration restarts, code is verified, code is
  resent, registration completes, another registration completes first, code
  expires, or the server restarts.
- Guards: starting registration rejects emails and usernames already owned by
  persisted users; a pending challenge does not own its requested username;
  operations for one email are serialized so a stale request cannot overwrite
  or remove a newer challenge; completion requires the matching unexpired code
  and rechecks persisted email and username ownership while registration
  completions are serialized.
- Transitions: `available` moves to `pending_verification` after a valid start;
  restart or resend replaces that email's pending challenge;
  `pending_verification` moves to `active_account` when completion persists the
  user; it moves to `conflict` when another completed account already owns the
  email or username; expiry moves it to `expired` and cleanup returns the name
  to `available`.
- Side effects: start and resend send a code and update only process-local
  pending state through bounded email-scoped gates; completion writes one
  student user, removes its pending state, and issues an authentication token.
- Failure paths: invalid or exhausted codes do not create users; completion
  conflicts return the existing `EMAIL_TAKEN` or `USERNAME_TAKEN` response and
  remove the stale pending challenge. Verification and completion code checks
  share the same bounded attempt counter.
- Rollback paths: restore pending-username collision checks at registration
  start. No stored data or schema rollback is required because pending
  challenges are not persisted.

## Impact Surface

- Callers: the existing registration start, verification, resend, and
  completion endpoints; request and response shapes do not change.
- Dependencies: process-local pending registration storage, bounded
  email-scoped synchronization, registration-completion coordination, and the
  existing EF Core user queries.
- Data: pending challenges may share a requested username; persisted users
  remain the source of truth and retain unique email and username constraints.
- Permissions: public student self-registration remains the only affected
  permission path; role assignment and administrator creation do not change.
- Deployment: no migration or new configuration is required.

## Primitive Acceptance Criteria

- Abandoning an uncompleted verification challenge does not make its requested
  username unavailable to another registration start.
- Starting a second pending challenge for the same username does not create a
  user or invalidate the first challenge.
- A persisted user's username remains unavailable to registration starts.
- If two pending challenges request the same username, the first successful
  completion creates the account and the later completion reports
  `USERNAME_TAKEN` without creating another user, including when completion
  requests arrive concurrently or use different username casing.
- Concurrent invalid-code attempts count independently and cannot overwrite a
  newer challenge or bypass the attempt limit.
- Existing registration routes and response shapes remain unchanged.
