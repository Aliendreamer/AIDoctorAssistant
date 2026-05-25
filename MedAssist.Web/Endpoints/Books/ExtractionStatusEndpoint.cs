using FastEndpoints;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class ExtractionStatusEndpoint : EndpointWithoutRequest
{
    private readonly ExtractionTracker _tracker;

    public ExtractionStatusEndpoint(ExtractionTracker tracker) => _tracker = tracker;

    public override void Configure()
    {
        Get("/api/admin/books/extract/status");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idParam = Query<string>("id");

        if (!string.IsNullOrEmpty(idParam))
        {
            if (!int.TryParse(idParam, out var id))
            {
                await Send.ResponseAsync(new { message = "Query parameter 'id' must be a positive integer." }, 400, ct);
                return;
            }

            var entry = _tracker.Get(id);
            if (entry is null)
            {
                await Send.ResponseAsync(new { message = $"No extraction record for book id {id}." }, 404, ct);
                return;
            }

            await Send.OkAsync(MapEntry(id, entry), ct);
            return;
        }

        var all = _tracker.GetAll()
            .OrderBy(kv => kv.Value.StartedAt)
            .Select(kv => MapEntry(kv.Key, kv.Value))
            .ToList();

        await Send.OkAsync(all, ct);
    }

    private static object MapEntry(int id, ExtractionEntry e) => new
    {
        bookDbId = id,
        bookId = e.BookId,
        state = e.State.ToString(),
        startedAt = e.StartedAt,
        completedAt = e.CompletedAt,
        error = e.Error
    };
}
