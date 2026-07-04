using MedAssist.Data;
using MedAssist.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Data;

public sealed class UserRepository(MedAssistDbContext db, IPasswordHasher<UserEntity> hasher)
{
    public async Task<UserEntity?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower(), ct);
    }

    public async Task<IReadOnlyList<UserEntity>> ListAsync(CancellationToken ct = default)
    {
        // Order client-side: the users table is tiny and admin-only, and SQLite (the test provider)
        // cannot ORDER BY a DateTimeOffset — sorting in memory is identical on both providers.
        var users = await db.Users.ToListAsync(ct);
        return users.OrderBy(u => u.CreatedAt).ToList();
    }

    public async Task<UserEntity> CreateAsync(string username, string role, string plainPassword, CancellationToken ct = default)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = username,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = hasher.HashPassword(user, plainPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rows = await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<int> CountAdminsAsync(CancellationToken ct = default)
    {
        return await db.Users.CountAsync(u => u.Role == "Admin", ct);
    }

    public bool VerifyPassword(UserEntity user, string plainPassword)
    {
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, plainPassword);
        return result != PasswordVerificationResult.Failed;
    }
}
