## ADDED Requirements

### Requirement: Cookie-based login for Blazor pages
The system SHALL authenticate Blazor page users via an ASP.NET Core cookie scheme. A `POST /login` form SHALL validate credentials against the `Auth:Users` configuration list (same list used by the JWT login endpoint) and issue a session cookie on success. The cookie SHALL carry the user's username and role as claims.

#### Scenario: Valid doctor login
- **WHEN** a user submits `/login` with a username and password matching a configured Doctor-role user
- **THEN** the system issues an authentication cookie and redirects to `/query`

#### Scenario: Valid admin login
- **WHEN** a user submits `/login` with a username and password matching a configured Admin-role user
- **THEN** the system issues an authentication cookie and redirects to `/query`

#### Scenario: Invalid credentials
- **WHEN** a user submits `/login` with a username or password that does not match any configured user
- **THEN** the login page re-renders with the message "Invalid username or password" and no cookie is issued

### Requirement: Unauthenticated redirect
The system SHALL redirect any unauthenticated request to a protected Blazor page to `/login`. The redirect SHALL occur at the routing layer via `AuthorizeRouteView`, not via middleware.

#### Scenario: Unauthenticated access to query page
- **WHEN** an unauthenticated browser navigates to `/query`
- **THEN** the system renders the not-authorized content which navigates to `/login`

#### Scenario: Unauthenticated access to admin area
- **WHEN** an unauthenticated browser navigates to `/admin/books`
- **THEN** the system renders the not-authorized content which navigates to `/login`

### Requirement: Logout
The system SHALL provide a logout action that clears the authentication cookie and redirects the browser to `/login` via a full page load.

#### Scenario: Logout clears session
- **WHEN** an authenticated user triggers the logout action from the nav
- **THEN** the authentication cookie is removed and the browser is redirected to `/login`

#### Scenario: Post-logout access denied
- **WHEN** a user visits `/query` after logging out
- **THEN** the system redirects to `/login`

### Requirement: JWT scheme unchanged for REST API
The system SHALL leave JWT bearer as the authentication scheme for all FastEndpoints REST endpoints. Cookie auth SHALL apply only to Blazor interactive routes.

#### Scenario: API call without cookie
- **WHEN** a client sends `POST /api/admin/books/upload` with a valid JWT bearer token but no cookie
- **THEN** the endpoint accepts the request and returns 200

#### Scenario: API call without JWT
- **WHEN** a client sends `POST /api/admin/books/upload` with no Authorization header
- **THEN** the endpoint returns 401
