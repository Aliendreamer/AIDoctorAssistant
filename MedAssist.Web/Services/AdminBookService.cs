using MedAssist.Shared.Models;

namespace MedAssist.Web.Services;

/// <summary>
/// Blazor-facing adapter for admin book operations. Delegates in-process to
/// <see cref="BookCatalogService"/> and <see cref="BookApplicationService"/> — the same services the
/// REST endpoints use — instead of calling the app's own API over loopback (audit P1-12). Keeps the
/// tuple-returning signatures the admin pages consume so the page code is unchanged.
/// </summary>
public sealed class AdminBookService(BookCatalogService catalog, BookApplicationService books)
{
    private readonly BookCatalogService _catalog = catalog;
    private readonly BookApplicationService _books = books;

    public async Task<IReadOnlyList<BookInfo>> GetBooksAsync(CancellationToken ct = default)
        => await _catalog.GetAllBooksAsync(ct);

    public async Task<(bool Success, string Message)> TriggerReindexAsync(int bookId, CancellationToken ct = default)
    {
        var result = await _books.TriggerIndexAsync(bookId, force: true, ct);
        return (result.Outcome == TriggerIndexOutcome.Started, result.Message);
    }

    public async Task<(bool Success, string Message)> UploadBookAsync(
        string bookId, string title, string author, string language, string edition,
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var result = await _books.UploadAsync(
            new UploadBookInput(bookId, title, author, language, edition, fileStream, fileName), ct);
        return (result.Success, result.Message);
    }
}
