using FastEndpoints;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Users;

public sealed class CreateUserRequest
{
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class CreateUserEndpoint : Endpoint<CreateUserRequest>
{
    private readonly UserApplicationService _users;

    public CreateUserEndpoint(UserApplicationService users) => _users = users;

    public override void Configure()
    {
        Post("/api/admin/users");
        Roles("Admin");
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var result = await _users.CreateAsync(req.Username, req.Role, req.Password, ct);
        switch (result.Outcome)
        {
            case CreateUserOutcome.Created:
                var user = result.User!;
                await Send.ResponseAsync(new UserDto(user.Id, user.Username, user.Role, user.CreatedAt), 201, ct);
                break;
            case CreateUserOutcome.WeakPassword:
            case CreateUserOutcome.InvalidRole:
                await Send.ResponseAsync(result.Message, 400, ct);
                break;
            case CreateUserOutcome.DuplicateUsername:
                await Send.ResponseAsync(result.Message, 409, ct);
                break;
        }
    }
}
