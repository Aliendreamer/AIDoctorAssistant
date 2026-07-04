using FastEndpoints;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Users;

public sealed class DeleteUserEndpoint : EndpointWithoutRequest
{
    private readonly UserApplicationService _users;

    public DeleteUserEndpoint(UserApplicationService users) => _users = users;

    public override void Configure()
    {
        Delete("/api/admin/users/{id}");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(Route<string>("id"), out var id))
        {
            await Send.ResponseAsync("Invalid user id.", 400, ct);
            return;
        }

        var result = await _users.DeleteAsync(id, ct);
        switch (result.Outcome)
        {
            case DeleteUserOutcome.Deleted:
                await Send.NoContentAsync(ct);
                break;
            case DeleteUserOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                break;
            case DeleteUserOutcome.LastAdmin:
                await Send.ResponseAsync(result.Message, 409, ct);
                break;
        }
    }
}
