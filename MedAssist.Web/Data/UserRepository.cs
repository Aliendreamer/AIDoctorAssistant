using MedAssist.Data;
using MedAssist.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Data;

public sealed class UserRepository
{
    private readonly IDbContextFactory<MedAssistDbContext> _dbFactory;
    private readonly IPasswordHasher<UserEntity> _hasher;

    public UserRepository(IDbContextFactory<MedAssistDbContext> dbFactory, IPasswordHasher<UserEntity> hasher)
    {
        _dbFactory = dbFactory;
        _hasher = hasher;
    }

    public async Task<UserEntity?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);
    }

    public async Task<IReadOnlyList<UserEntity>> ListAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Users.OrderBy(u => u.CreatedAt).ToListAsync(ct);
    }

    public async Task<UserEntity> CreateAsync(string username, string role, string plainPassword, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = username,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, plainPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<int> CountAdminsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Users.CountAsync(u => u.Role == "Admin", ct);
    }

    public bool VerifyPassword(UserEntity user, string plainPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, plainPassword);
        return result != PasswordVerificationResult.Failed;
    }
}
