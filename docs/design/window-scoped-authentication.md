# Window-scoped authentication

## Problem definition

Authentication tokens and users were stored in `localStorage` when "Remember
me" was selected. Because `localStorage` is shared by every same-origin browser
window, a student login could replace an administrator login and logout could
clear both windows. The application must allow an administrator and a student
to remain signed in concurrently in separate windows.

## Option comparison

- Shared `localStorage`: preserves long-lived device login, but cannot isolate
  accounts or logout by window.
- Browser profiles or private browsing: isolates accounts without code changes,
  but makes the application behavior depend on an external browser workflow.
- Window `sessionStorage`: gives every top-level browser context its own token
  and user, preserves navigation and reloads within that window, and allows the
  backend to revoke only the token used by that window. It does not preserve a
  login after that window is closed.

The application uses window `sessionStorage`. The login and registration UI no
longer promise device-level persistence.

## State machine

States:

- `uninitialized`: the window has not inspected auth storage.
- `unauthenticated`: the window has no usable token and user.
- `authenticated_student`: the window holds a student token and user.
- `authenticated_administrator`: the window holds an administrator token and user.
- `expired`: the backend rejected the window token with HTTP 401.

Events and transitions:

- First auth read with legacy shared credentials: remove the shared copy and
  transition to `unauthenticated`, requiring one fresh window-specific login.
- Successful login, registration, or Google callback: write the returned token
  and normalized user to the current window and transition to the matching
  authenticated state.
- Explicit logout: send the current window token to backend logout, clear the
  current window immediately, and transition to `unauthenticated` even if the
  backend is unavailable.
- HTTP 401: clear only the current window and transition through `expired` to
  `unauthenticated`.
- Window close: browser-managed `sessionStorage` removal transitions that
  window to `unauthenticated`; other windows are unchanged.

Guards:

- Stored users must contain an identifier, email, and supported role.
- API requests attach only the token selected from the current window.
- A partial Google callback token is rolled back if `/auth/me` fails.

Side effects and failure paths:

- Logout revokes only the bearer token attached by the current window.
- Local clearing never waits for the backend.
- Invalid stored user JSON clears only the current window.
- Backend and role authorization remain authoritative.

## Impact surface

- Frontend auth storage and API login/callback/registration calls.
- Login and registration persistence copy.
- Authentication acceptance and test documentation.
- No backend endpoint, RBAC, database, assessment attempt, sandbox, or AI
  boundary changes.

## Rollback path

Restore the previous local/session storage selection in `authStorage.ts`,
restore the persistence controls, and remove this design rule. Existing
window-scoped entries disappear when their windows close.

## Primitive acceptance criteria

- An administrator and a student can authenticate in separate same-origin
  browser windows and both can use their authorized pages concurrently.
- Logging out or receiving HTTP 401 in one window leaves every other window's
  local auth state unchanged.
- Reloading or navigating within an authenticated window preserves that
  window’s account.
- Closing an authenticated window removes that window’s login persistence.
- Logout still attempts backend revocation for the current window token.
