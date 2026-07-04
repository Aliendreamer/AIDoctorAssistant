namespace MedAssist.Web.Services;

/// <summary>
/// Blazor-facing adapter for admin user operations. Delegates in-process to
/// <see cref="UserApplicationService"/> — the same service the REST user endpoints use — instead of
/// calling the app's own API over loopback (audit P1-12). Keeps the tuple-returning signatures the
/// admin pages consume so the page code is unchanged.
/// </summary>
public sealed class AdminUserService(UserApplicationService users)
{
    private readonly UserApplicationService _users = users;

    public async Task<IReadOnlyList<UserInfo>> GetUsersAsync(CancellationToken ct = default)
        => await _users.ListAsync(ct);

    public async Task<(bool Success, string Message)> CreateUserAsync(
        string username, string role, string password, CancellationToken ct = default)
    {
        var result = await _users.CreateAsync(username, role, password, ct);
        return (result.Outcome == CreateUserOutcome.Created, result.Message);
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var result = await _users.DeleteAsync(id, ct);
        return (result.Outcome == DeleteUserOutcome.Deleted, result.Message);
    }
}
