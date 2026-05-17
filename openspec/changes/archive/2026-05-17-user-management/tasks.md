## 1. Data Layer

- [x] 1.1 Create `MedAssist.Data/Entities/UserEntity.cs` — properties: `Guid Id`, `string Username`, `string PasswordHash`, `string Role`, `DateTimeOffset CreatedAt`
- [x] 1.2 Create `MedAssist.Data/Configuration/UserEntityConfiguration.cs` — table `users`, UUID pk with `ValueGeneratedNever()`, unique index on `username`, `role` and `created_at` columns
- [x] 1.3 Add `public DbSet<UserEntity> Users => Set<UserEntity>();` to `MedAssistDbContext`
- [x] 1.4 Run EF Core migration: `dotnet ef migrations add AddUsersTable --project MedAssist.Data --startup-project MedAssist.Data`

## 2. User Repository and Password Hashing

- [x] 2.1 Create `MedAssist.Web/Data/UserRepository.cs` — methods: `FindByUsernameAsync`, `ListAsync`, `CreateAsync`, `DeleteAsync`, `AdminCountAsync`; inject `IDbContextFactory<MedAssistDbContext>` and `IPasswordHasher<UserEntity>`
- [x] 2.2 Register `IPasswordHasher<UserEntity>` (via `services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>()`) and `UserRepository` as transient in `ServiceCollectionExtensions.AddDataServices`

## 3. First-Run Seed

- [x] 3.1 Create `MedAssist.Web/Startup/UserSeeder.cs` — checks admin count; if zero, reads first Admin from `Auth:Users` config, hashes password, inserts; throws `InvalidOperationException` if neither DB admin nor config admin exists
- [x] 3.2 Call `await app.SeedUsersAsync()` in `Program.cs` after `MigrateDbAsync()`

## 4. Login Endpoint — DB Validation

- [x] 4.1 Inject `UserRepository` into `LoginEndpoint` (FastEndpoints JWT login); replace config-list lookup with `UserRepository.FindByUsernameAsync`; use `PasswordHasher.VerifyHashedPassword` to check the password
- [x] 4.2 Update the Blazor `Login.razor` page (from blazor-ui change) to use `UserRepository` instead of config list for cookie sign-in

## 5. Admin User REST Endpoints

- [x] 5.1 Create `MedAssist.Web/Endpoints/Users/ListUsersEndpoint.cs` — `GET /api/admin/users`, requires Admin role, returns list of `{ Id, Username, Role, CreatedAt }`
- [x] 5.2 Create `MedAssist.Web/Endpoints/Users/CreateUserEndpoint.cs` — `POST /api/admin/users`, requires Admin role, validates username uniqueness and password length (min 8), returns 201 with user id
- [x] 5.3 Create `MedAssist.Web/Endpoints/Users/DeleteUserEndpoint.cs` — `DELETE /api/admin/users/{id}`, requires Admin role, checks admin count before deleting, returns 204/404/409

## 6. Admin User Service (Blazor)

- [x] 6.1 Extract shared JWT self-auth logic from `AdminBookService` into `MedAssist.Web/Services/AdminApiClient.cs` — obtains and caches a JWT by calling `POST /api/auth/login` with admin credentials from config
- [x] 6.2 Update `AdminBookService` to use `AdminApiClient` for its bearer token
- [x] 6.3 Create `MedAssist.Web/Services/AdminUserService.cs` — uses `AdminApiClient`; implements `GetUsersAsync`, `CreateUserAsync(username, role, password)`, `DeleteUserAsync(id)`
- [x] 6.4 Register `AdminApiClient` and `AdminUserService` as scoped in `ServiceCollectionExtensions.AddQueryServices`

## 7. Admin Blazor Pages

- [x] 7.1 Create `MedAssist.Web/Components/Pages/Admin/Users.razor` at route `/admin/users` with `@attribute [Authorize(Roles="Admin")]`; load users from `AdminUserService.GetUsersAsync()`; render table (username, role, created date) with Delete button per row; Add User button links to `/admin/users/create`
- [x] 7.2 Wire Delete button: show inline confirmation, call `AdminUserService.DeleteUserAsync(id)`, handle 409 "last admin" error inline, refresh list on success
- [x] 7.3 Create `MedAssist.Web/Components/Pages/Admin/CreateUser.razor` at route `/admin/users/create` with `@attribute [Authorize(Roles="Admin")]`; form: username, role dropdown, password, confirm password; client-side password match check; submit calls `AdminUserService.CreateUserAsync`; navigate to `/admin/users` on success; show API error inline on failure
- [x] 7.4 Add "Users" link to `AdminLayout.razor` nav pointing to `/admin/users`
