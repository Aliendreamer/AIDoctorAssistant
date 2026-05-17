using FastEndpoints;
using MedAssist.Web.Data;

namespace MedAssist.Web.Endpoints.Users;

public sealed class DeleteUserEndpoint : EndpointWithoutRequest
{
    private readonly UserRepository _users;

    public DeleteUserEndpoint(UserRepository users) => _users = users;

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

        var allUsers = await _users.ListAsync(ct);
        var target = allUsers.FirstOrDefault(u => u.Id == id);

        if (target is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (target.Role == "Admin")
        {
            var adminCount = await _users.CountAdminsAsync(ct);
            if (adminCount <= 1)
            {
                await Send.ResponseAsync("Cannot delete the last Admin account.", 409, ct);
                return;
            }
        }

        await _users.DeleteAsync(id, ct);
        await Send.NoContentAsync(ct);
    }
}
