namespace MedAssist.Web.Services;

public sealed record UserInfo(Guid Id, string Username, string Role, DateTimeOffset CreatedAt);

public sealed class AdminUserService
{
    private readonly AdminApiClient _client;

    public AdminUserService(AdminApiClient client) => _client = client;

    public async Task<IReadOnlyList<UserInfo>> GetUsersAsync(CancellationToken ct = default)
    {
        await _client.EnsureAuthenticatedAsync(ct);
        var list = await _client.Http.GetFromJsonAsync<List<UserInfo>>("/api/admin/users", ct);
        return list ?? [];
    }

    public async Task<(bool Success, string Message)> CreateUserAsync(
        string username, string role, string password, CancellationToken ct = default)
    {
        await _client.EnsureAuthenticatedAsync(ct);
        var response = await _client.Http.PostAsJsonAsync("/api/admin/users",
            new { username, role, password }, ct);

        if (response.IsSuccessStatusCode)
        {
            return (true, string.Empty);
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)response.StatusCode}" : body);
    }

    public async Task<(bool Success, string Message)> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        await _client.EnsureAuthenticatedAsync(ct);
        var response = await _client.Http.DeleteAsync($"/api/admin/users/{id}", ct);

        if (response.IsSuccessStatusCode)
        {
            return (true, string.Empty);
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)response.StatusCode}" : body);
    }
}
