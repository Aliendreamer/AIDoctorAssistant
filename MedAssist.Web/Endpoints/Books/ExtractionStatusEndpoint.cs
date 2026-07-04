using FastEndpoints;
using MedAssist.Shared.Models;
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

            var entry = await _tracker.GetAsync(id, ct);
            if (entry is null)
            {
                await Send.ResponseAsync(new { message = $"No extraction record for book id {id}." }, 404, ct);
                return;
            }

            await Send.OkAsync(MapEntry(entry), ct);
            return;
        }

        var all = (await _tracker.GetAllAsync(ct))
            .Select(MapEntry)
            .ToList();

        await Send.OkAsync(all, ct);
    }

    private static object MapEntry(ExtractionEntry e) => new
    {
        bookDbId = e.BookDbId,
        bookId = e.BookId,
        state = e.State.ToString(),
        startedAt = e.StartedAt,
        completedAt = e.CompletedAt,
        error = e.Error
    };
}
