using FastEndpoints;
using MedAssist.Web.Data;

namespace MedAssist.Web.Endpoints.Users;

public sealed record UserDto(Guid Id, string Username, string Role, DateTimeOffset CreatedAt);

public sealed class ListUsersEndpoint : EndpointWithoutRequest<IReadOnlyList<UserDto>>
{
    private readonly UserRepository _users;

    public ListUsersEndpoint(UserRepository users) => _users = users;

    public override void Configure()
    {
        Get("/api/admin/users");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var users = await _users.ListAsync(ct);
        var dtos = users.Select(u => new UserDto(u.Id, u.Username, u.Role, u.CreatedAt)).ToList();
        await HttpContext.Response.SendAsync(dtos, cancellation: ct);
    }
}
