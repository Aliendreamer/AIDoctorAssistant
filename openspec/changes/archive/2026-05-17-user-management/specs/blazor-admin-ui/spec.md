## ADDED Requirements

### Requirement: User list page (Admin only)
The `/admin/users` page SHALL require the Admin role. It SHALL display a table of all user accounts with columns: username, role, and created date. Each row SHALL include a "Delete" button. A prominent "Add User" button SHALL link to `/admin/users/create`. The page SHALL load data from `AdminUserService.GetUsersAsync()`.

#### Scenario: Admin views user list
- **WHEN** an Admin-role user navigates to `/admin/users`
- **THEN** the page shows a table row for each user in the system

#### Scenario: Doctor denied access
- **WHEN** a Doctor-role user navigates to `/admin/users`
- **THEN** the page shows "Access Denied"

### Requirement: Create user page (Admin only)
The `/admin/users/create` page SHALL require the Admin role. It SHALL provide a form with: username (text, required), role dropdown (Doctor / Admin, required), password (text, required, min 8 chars), confirm password (must match). Submitting SHALL call `AdminUserService.CreateUserAsync`. On success the page navigates to `/admin/users`. On error it shows the API error message inline.

#### Scenario: Create valid Doctor account
- **WHEN** an Admin fills the form with a unique username, role Doctor, and matching passwords of at least 8 chars and submits
- **THEN** the user is created and the admin is redirected to `/admin/users`

#### Scenario: Password mismatch blocked client-side
- **WHEN** an Admin submits the form with password and confirm password that do not match
- **THEN** the form shows "Passwords do not match" and does not call the API

#### Scenario: Duplicate username error shown
- **WHEN** an Admin submits the form with a username that already exists
- **THEN** the page shows the API's 409 error message inline

### Requirement: Delete user with guard (Admin only)
Clicking "Delete" on a user row SHALL show a confirmation prompt. On confirmation it calls `AdminUserService.DeleteUserAsync(id)`. If the API returns 409 (last admin guard), the page shows "Cannot delete the last Admin account" inline.

#### Scenario: Delete Doctor with confirmation
- **WHEN** an Admin clicks "Delete" on a Doctor row and confirms
- **THEN** the user is removed and the row disappears from the table

#### Scenario: Last admin delete blocked
- **WHEN** an Admin clicks "Delete" on the only Admin row and confirms
- **THEN** the page shows "Cannot delete the last Admin account" and the row remains

### Requirement: Admin navigation includes Users link
The admin nav (in `AdminLayout`) SHALL include a "Users" link pointing to `/admin/users`, alongside the existing "Books" and "Upload Book" links.

#### Scenario: Admin nav shows Users link
- **WHEN** an Admin-role user is in the admin area
- **THEN** the nav includes a "Users" link
