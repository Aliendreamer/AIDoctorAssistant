using MedAssist.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Services;

public sealed record UserInfo(Guid Id, string Username, string Role, DateTimeOffset CreatedAt);

public enum CreateUserOutcome { Created, WeakPassword, InvalidRole, DuplicateUsername }

public sealed record CreateUserResult(CreateUserOutcome Outcome, UserInfo? User, string Message);

public enum DeleteUserOutcome { Deleted, NotFound, LastAdmin }

public sealed record DeleteUserResult(DeleteUserOutcome Outcome, string Message);

/// <summary>
/// In-process application service owning admin user operations (list/create/delete) and their
/// validation — notably the last-admin guard. Both the REST user endpoints and the Blazor admin
/// pages (via <see cref="AdminUserService"/>) call this, so the rules live in exactly one place
/// (audit P1-12: admin UI runs in-process, no loopback API call).
/// </summary>
public sealed class UserApplicationService(UserRepository users)
{
    private readonly UserRepository _users = users;

    public async Task<IReadOnlyList<UserInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _users.ListAsync(cancellationToken);
        return entities.Select(u => new UserInfo(u.Id, u.Username, u.Role, u.CreatedAt)).ToList();
    }

    public async Task<CreateUserResult> CreateAsync(string username, string role, string password, CancellationToken cancellationToken = default)
    {
        if (password.Length < 8)
        {
            return new CreateUserResult(CreateUserOutcome.WeakPassword, null, "Password must be at least 8 characters.");
        }

        if (role is not "Admin" and not "Doctor")
        {
            return new CreateUserResult(CreateUserOutcome.InvalidRole, null, "Role must be Admin or Doctor.");
        }

        try
        {
            var user = await _users.CreateAsync(username, role, password, cancellationToken);
            var info = new UserInfo(user.Id, user.Username, user.Role, user.CreatedAt);
            return new CreateUserResult(CreateUserOutcome.Created, info, "User created.");
        }
        catch (DbUpdateException)
        {
            return new CreateUserResult(CreateUserOutcome.DuplicateUsername, null, "A user with that username already exists.");
        }
    }

    public async Task<DeleteUserResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var all = await _users.ListAsync(cancellationToken);
        var target = all.FirstOrDefault(u => u.Id == id);
        if (target is null)
        {
            return new DeleteUserResult(DeleteUserOutcome.NotFound, "User not found.");
        }

        // Never orphan the system: the last Admin cannot be removed (audit P1-12 rule, centralized).
        if (target.Role == "Admin" && await _users.CountAdminsAsync(cancellationToken) <= 1)
        {
            return new DeleteUserResult(DeleteUserOutcome.LastAdmin, "Cannot delete the last Admin account.");
        }

        await _users.DeleteAsync(id, cancellationToken);
        return new DeleteUserResult(DeleteUserOutcome.Deleted, "User deleted.");
    }
}
