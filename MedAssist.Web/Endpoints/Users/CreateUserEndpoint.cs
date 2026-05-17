using FastEndpoints;
using MedAssist.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Endpoints.Users;

public sealed class CreateUserRequest
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class CreateUserEndpoint : Endpoint<CreateUserRequest>
{
    private readonly UserRepository _users;

    public CreateUserEndpoint(UserRepository users) => _users = users;

    public override void Configure()
    {
        Post("/api/admin/users");
        Roles("Admin");
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        if (req.Password.Length < 8)
        {
            await Send.ResponseAsync("Password must be at least 8 characters.", 400, ct);
            return;
        }

        if (req.Role is not "Admin" and not "Doctor")
        {
            await Send.ResponseAsync("Role must be Admin or Doctor.", 400, ct);
            return;
        }

        try
        {
            var user = await _users.CreateAsync(req.Username, req.Role, req.Password, ct);
            var dto = new UserDto(user.Id, user.Username, user.Role, user.CreatedAt);
            await Send.ResponseAsync(dto, 201, ct);
        }
        catch (DbUpdateException)
        {
            await Send.ResponseAsync("A user with that username already exists.", 409, ct);
        }
    }
}
