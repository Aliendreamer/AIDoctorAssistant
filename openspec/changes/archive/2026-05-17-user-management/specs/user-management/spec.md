## ADDED Requirements

### Requirement: Persistent user store with hashed passwords
The system SHALL store user accounts in a `users` PostgreSQL table. Each user record SHALL include a UUID primary key, a unique username, a PBKDF2-SHA512 password hash (via ASP.NET Core `PasswordHasher`), a role (Doctor or Admin), and a created_at timestamp. Plaintext passwords SHALL never be stored.

#### Scenario: User created with hashed password
- **WHEN** an admin creates a user with a plaintext password
- **THEN** only the PBKDF2 hash is stored in the database; the plaintext is not persisted

#### Scenario: Duplicate username rejected
- **WHEN** an admin attempts to create a user with a username that already exists
- **THEN** the system returns a 409 Conflict error and does not create a second record

### Requirement: First-run admin seed
On application startup, if no Admin-role user exists in the `users` table, the system SHALL read the first Admin entry from the `Auth:Users` configuration list, hash its password, and insert it as the initial Admin account. If no Admin entry exists in config and no Admin exists in DB, the system SHALL throw a startup exception.

#### Scenario: Fresh database seeded from config
- **WHEN** the application starts with an empty `users` table
- **THEN** the system inserts the first Admin user from `Auth:Users` config with a hashed password

#### Scenario: Subsequent starts skip seed
- **WHEN** the application starts and at least one Admin already exists in `users`
- **THEN** no seed insert is performed

### Requirement: List users (Admin only)
`GET /api/admin/users` SHALL return a list of all users. Each entry SHALL include: id (UUID), username, role, and createdAt. Password hashes SHALL NOT appear in the response. The endpoint SHALL require the Admin role.

#### Scenario: Admin lists users
- **WHEN** an Admin sends `GET /api/admin/users` with a valid JWT
- **THEN** the response is 200 with an array of user objects (no password field)

#### Scenario: Doctor denied
- **WHEN** a Doctor sends `GET /api/admin/users` with a valid JWT
- **THEN** the response is 403

### Requirement: Create user (Admin only)
`POST /api/admin/users` SHALL accept a JSON body with `username`, `role`, and `password`. The system SHALL validate that the username is not already taken and that the role is one of "Doctor" or "Admin". On success it SHALL return 201 with the created user's id. The endpoint SHALL require the Admin role.

#### Scenario: Admin creates a Doctor account
- **WHEN** an Admin POSTs `{ "username": "dr_ivanov", "role": "Doctor", "password": "secret" }` with a valid JWT
- **THEN** the system returns 201 and the new user appears in `GET /api/admin/users`

#### Scenario: Password too short
- **WHEN** an Admin POSTs a new user with `password` shorter than 8 characters
- **THEN** the system returns 400 with a validation error

### Requirement: Delete user (Admin only)
`DELETE /api/admin/users/{id}` SHALL remove the user with the given UUID. The system SHALL reject the deletion if it would leave zero Admin-role users in the system. The endpoint SHALL require the Admin role.

#### Scenario: Admin deletes a Doctor
- **WHEN** an Admin sends `DELETE /api/admin/users/{doctorId}` with a valid JWT
- **THEN** the system returns 204 and the user no longer appears in the list

#### Scenario: Last admin cannot be deleted
- **WHEN** an Admin sends `DELETE /api/admin/users/{id}` for the only remaining Admin account
- **THEN** the system returns 409 with the message "Cannot delete the last Admin account"

#### Scenario: Delete non-existent user
- **WHEN** an Admin sends `DELETE /api/admin/users/{unknownId}` for a UUID that does not exist
- **THEN** the system returns 404
