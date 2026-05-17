## Why

Users are currently hardcoded in `appsettings.shared.json` under `Auth:Users`. Adding or removing a doctor requires editing a config file and redeploying. There is no way for an admin to manage users at runtime, and credentials are stored in plaintext in the config. The system needs a proper user store in the database with hashed passwords and an admin-only interface to create and manage accounts.

No self-registration is allowed — all accounts are created by an Admin. This keeps the system closed to unauthorized access, which is appropriate for a medical tool.

## What Changes

**User store**
- New `users` table in PostgreSQL with columns: id (uuid), username (unique), password_hash, role (Doctor | Admin), created_at
- Passwords stored as PBKDF2 hashes using ASP.NET Core's built-in `IPasswordHasher<T>`
- Seed the initial Admin account from config on first run (so the first deploy is not locked out)

**Auth changes**
- Login endpoint (both JWT for REST API and cookie for Blazor) validates against the `users` DB table instead of the config list
- Config `Auth:Users` list is used only for the first-run seed; afterwards the DB is authoritative

**Admin UI addition** (extends the blazor-ui change)
- `/admin/users` — list all users (username, role, created date); Add User button; Delete button per user
- `/admin/users/create` — form to create a new user: username, role dropdown (Doctor/Admin), password, confirm password
- No edit-user page; to change a password the admin deletes and recreates the account

## Capabilities

### New Capabilities

- `user-management`: Persistent user accounts with hashed passwords, admin-only CRUD

### Modified Capabilities

- `blazor-auth`: Login now validates against DB users instead of config list
- `blazor-admin-ui`: Admin nav gains a "Users" link; two new pages (list, create)

## Impact

- `MedAssist.Data/Entities/UserEntity.cs` — new
- `MedAssist.Data/Configuration/UserEntityConfiguration.cs` — new
- `MedAssist.Data/MedAssistDbContext.cs` — add `DbSet<UserEntity>`
- `MedAssist.Data/Migrations/` — new migration: create `users` table
- `MedAssist.Web/Extensions/ServiceCollectionExtensions.cs` — register `IPasswordHasher`, `UserRepository`
- `MedAssist.Web/Data/UserRepository.cs` — new: find by username, create, delete, list
- `MedAssist.Web/Startup/UserSeeder.cs` — new: seeds Admin from config on first run
- `MedAssist.Web/Endpoints/Auth/LoginEndpoint.cs` — validate against DB instead of config
- `MedAssist.Web/Components/Pages/Admin/Users.razor` — new
- `MedAssist.Web/Components/Pages/Admin/CreateUser.razor` — new
- `MedAssist.Web/Services/AdminUserService.cs` — new (wraps HTTP calls to user management API endpoints)
- New FastEndpoints: `GET /api/admin/users`, `POST /api/admin/users`, `DELETE /api/admin/users/{id}`
