using FastEndpoints;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class TriggerIndexEndpoint(BookApplicationService books) : EndpointWithoutRequest
{
    private readonly BookApplicationService _books = books;

    public override void Configure()
    {
        Post("/api/admin/index");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!int.TryParse(Query<string>("id"), out var id) || id <= 0)
        {
            await Send.ResponseAsync(new { message = "Query parameter 'id' must be a positive integer." }, 400, ct);
            return;
        }

        var force = Query<string>("force", isRequired: false)?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        var result = await _books.TriggerIndexAsync(id, force, ct);
        switch (result.Outcome)
        {
            case TriggerIndexOutcome.Started:
                await Send.ResponseAsync(new { message = result.Message }, 202, ct);
                break;
            case TriggerIndexOutcome.NotFound:
                await Send.NotFoundAsync(ct);
                break;
            case TriggerIndexOutcome.InvalidBookId:
                await Send.ResponseAsync(new { message = result.Message }, 400, ct);
                break;
            case TriggerIndexOutcome.AlreadyInProgress:
                await Send.ResponseAsync(new { message = result.Message }, 409, ct);
                break;
        }
    }
}
