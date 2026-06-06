# Local Demo Auth Experience

## Problem Definition

Local users need a stable sign-in flow after one-command startup. Signing in
must not briefly enter a protected page and then fall back to `/login`, and the
local default administrator account must be discoverable without reading
terminal output or `.env.local`. The one-command startup path must also repair
the local seed administrator before the login page offers those credentials.

## Option Comparison

- Clear auth whenever the login page mounts: rejected because any temporary
  navigation back to `/login` destroys a newly issued token and can create the
  observed flash-then-login behavior.
- Keep auth forever until explicit logout: rejected because backend restarts
  invalidate in-memory local tokens and would cause protected pages to loop.
- Clear auth only after backend 401 and provide a development-only local admin
  fill action: selected. It preserves valid same-tab sign-ins, removes stale
  tokens when the backend says they are invalid, and avoids exposing local demo
  credentials in production builds.
- Add an unauthenticated dev HTTP endpoint to repair the seed administrator:
  rejected because it would create a new security-sensitive API surface.
- Run the existing backend seeder in a CLI-only startup mode before local dev
  reuses or starts the backend: selected. It uses the same EF Core seed logic as
  normal backend startup and does not add another database dependency.
- Trust backend health alone when a process is already listening: rejected
  because an older local Backend process can be healthy while using stale code,
  database, or user secret configuration.

## State Machine

- States: local config ready, seed admin repair running, seed admin repaired,
  unauthenticated, login form ready, local demo credentials filled, signing in,
  authenticated, protected route loading, auth stale, wrong role, non-auth
  backend error, logged out.
- Events: `npm run dev` invoked, seed-only backend command succeeds, seed-only
  backend command fails, login page mounted, local demo fill clicked, sign-in
  submitted, backend login succeeds, backend login fails, protected API returns
  401, protected API returns 403, protected API returns non-auth error, logout
  clicked.
- Guards: frontend stores only backend-issued bearer tokens; protected pages
  trust stored role only for routing hints; backend remains authoritative for
  API access; development-only demo credentials must not appear in production
  builds.
- Transitions: local config ready runs seed admin repair before backend health
  startup; existing local Backend listeners are stopped when they can be safely
  identified, then backend startup is retried from the current checkout; reused
  external backend health requires seed administrator login verification;
  existing stored auth redirects away from login to the stored role
  dashboard; local demo fill populates the form only; successful login stores
  token and user then routes to the role dashboard; backend 401 clears stored
  auth and routes to login; wrong role routes to the other role dashboard;
  non-auth backend errors stay on the current page with the real error message.
- Side effects: run the backend EF Core seed path in CLI-only mode; write or
  clear localStorage auth keys; emit a same-tab auth change event; show
  development-only local administrator credentials.
- Failure paths: seed-only backend failure stops startup with the real command
  failure; an incompatible listener that cannot be safely identified or stopped
  stops startup with manual close guidance; invalid credentials keep the user on
  login with the backend error; stale tokens clear on 401; backend data
  failures do not destroy auth.
- Rollback path: remove the demo fill action and restore the previous login
  mount behavior, then remove this document update.

## Impact Surface

- Frontend API auth storage and same-tab auth notifications.
- Login page development-only local administrator fill action.
- Student and admin data pages that previously treated every data error as an
  authentication failure.
- Local startup script, backend CLI startup path, and stale local Next.js
  listener handling.
- Project acceptance and behavior test documentation.

## Primitive Acceptance Criteria

- Loading `/login` does not clear a valid stored token.
- Successful sign-in stores the backend token and routes to the user's role
  dashboard.
- Backend 401 responses clear stored auth before navigating to `/login`.
- Non-auth backend data errors do not clear stored auth and do not redirect to
  `/login`.
- `npm run dev` runs backend seed repair before backend health reuse/start so
  the displayed local administrator credentials are actually valid.
- Existing local Backend listeners are restarted when safely identifiable so the
  API serves the current checkout; reused external backends must accept the
  configured seed administrator.
- Old local Next.js listeners on the frontend port are restarted when safely
  identifiable so `/login` serves the current checkout.
- Local development login shows a quick local administrator fill action using
  `admin@example.com` and `Admin123!`.
- Production login builds do not expose the local administrator fill action.
