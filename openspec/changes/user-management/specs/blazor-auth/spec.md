## MODIFIED Requirements

### Requirement: Cookie-based login for Blazor pages
The system SHALL authenticate Blazor page users via an ASP.NET Core cookie scheme. A `POST /login` form SHALL validate credentials against the `users` database table (not the config list). Password verification SHALL use `PasswordHasher.VerifyHashedPassword`. On success the system issues a session cookie carrying the user's username and role as claims. On failure it re-renders with "Invalid username or password".

#### Scenario: Valid doctor login
- **WHEN** a user submits `/login` with a username and password matching a Doctor-role user in the `users` table
- **THEN** the system issues an authentication cookie and redirects to `/query`

#### Scenario: Valid admin login
- **WHEN** a user submits `/login` with a username and password matching an Admin-role user in the `users` table
- **THEN** the system issues an authentication cookie and redirects to `/query`

#### Scenario: Invalid credentials
- **WHEN** a user submits `/login` with credentials that do not match any user in the `users` table
- **THEN** the login page re-renders with "Invalid username or password" and no cookie is issued
