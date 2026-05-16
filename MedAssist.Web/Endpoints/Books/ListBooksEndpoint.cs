using FastEndpoints;
using MedAssist.Shared.Models;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class ListBooksEndpoint : EndpointWithoutRequest<IReadOnlyList<BookInfo>>
{
    private readonly BookCatalogService _bookCatalog;

    public ListBooksEndpoint(BookCatalogService bookCatalog)
    {
        _bookCatalog = bookCatalog;
    }

    public override void Configure()
    {
        Get("/api/books");
        Roles("Admin", "Doctor");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var books = await _bookCatalog.GetAllBooksAsync(ct);
        await HttpContext.Response.SendAsync(books, cancellation: ct);
    }
}
