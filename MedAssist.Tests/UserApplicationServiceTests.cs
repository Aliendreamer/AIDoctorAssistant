using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Web.Data;
using MedAssist.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Tests;

// UserApplicationService centralizes the admin user rules (password/role validation, the last-admin
// guard) shared by the REST endpoints and the Blazor admin pages (audit P1-12). Runs against real
// SQLite with a real password hasher and unique index so the validation and DB constraints are
// actually exercised.
public sealed class UserApplicationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MedAssistDbContext _db;
    private readonly UserApplicationService _sut;

    public UserApplicationServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MedAssistDbContext(options);

        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE users (id TEXT NOT NULL PRIMARY KEY, username TEXT NOT NULL, " +
            "password_hash TEXT NOT NULL, role TEXT NOT NULL, created_at TEXT NOT NULL);");
        _db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX ix_users_username ON users (username);");

        var repo = new UserRepository(_db, new PasswordHasher<UserEntity>());
        _sut = new UserApplicationService(repo);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Create_ValidDoctor_Succeeds()
    {
        var result = await _sut.CreateAsync("drhouse", "Doctor", "password123");

        Assert.Equal(CreateUserOutcome.Created, result.Outcome);
        Assert.NotNull(result.User);
        Assert.Equal("drhouse", result.User!.Username);
        Assert.Single(await _sut.ListAsync());
    }

    [Fact]
    public async Task Create_ShortPassword_Rejected()
    {
        var result = await _sut.CreateAsync("drhouse", "Doctor", "short");

        Assert.Equal(CreateUserOutcome.WeakPassword, result.Outcome);
        Assert.Empty(await _sut.ListAsync());
    }

    [Fact]
    public async Task Create_UnknownRole_Rejected()
    {
        var result = await _sut.CreateAsync("drhouse", "Wizard", "password123");

        Assert.Equal(CreateUserOutcome.InvalidRole, result.Outcome);
    }

    [Fact]
    public async Task Create_DuplicateUsername_Rejected()
    {
        await _sut.CreateAsync("drhouse", "Doctor", "password123");

        var result = await _sut.CreateAsync("drhouse", "Admin", "password456");

        Assert.Equal(CreateUserOutcome.DuplicateUsername, result.Outcome);
    }

    [Fact]
    public async Task Delete_NonAdmin_Succeeds()
    {
        var created = await _sut.CreateAsync("drhouse", "Doctor", "password123");

        var result = await _sut.DeleteAsync(created.User!.Id);

        Assert.Equal(DeleteUserOutcome.Deleted, result.Outcome);
        Assert.Empty(await _sut.ListAsync());
    }

    [Fact]
    public async Task Delete_MissingUser_ReturnsNotFound()
    {
        var result = await _sut.DeleteAsync(Guid.NewGuid());

        Assert.Equal(DeleteUserOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Delete_LastAdmin_Blocked()
    {
        var admin = await _sut.CreateAsync("boss", "Admin", "password123");

        var result = await _sut.DeleteAsync(admin.User!.Id);

        Assert.Equal(DeleteUserOutcome.LastAdmin, result.Outcome);
        Assert.Single(await _sut.ListAsync());   // still there
    }

    [Fact]
    public async Task Delete_AdminWhenAnotherAdminExists_Succeeds()
    {
        var first = await _sut.CreateAsync("boss1", "Admin", "password123");
        await _sut.CreateAsync("boss2", "Admin", "password123");

        var result = await _sut.DeleteAsync(first.User!.Id);

        Assert.Equal(DeleteUserOutcome.Deleted, result.Outcome);
    }
}
