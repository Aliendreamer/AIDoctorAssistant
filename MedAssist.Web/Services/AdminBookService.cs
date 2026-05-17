using MedAssist.Shared.Models;

namespace MedAssist.Web.Services;

public sealed class AdminBookService
{
    private readonly AdminApiClient _client;

    public AdminBookService(AdminApiClient client) => _client = client;

    public async Task<IReadOnlyList<BookInfo>> GetBooksAsync(CancellationToken ct = default)
    {
        await _client.EnsureAuthenticatedAsync(ct);
        var response = await _client.Http.GetFromJsonAsync<List<BookInfo>>("/api/books", ct);
        return response ?? [];
    }

    public async Task<(bool Success, string Message)> TriggerReindexAsync(int bookId, CancellationToken ct = default)
    {
        await _client.EnsureAuthenticatedAsync(ct);
        var response = await _client.Http.PostAsync($"/api/admin/index?id={bookId}&force=true", content: null, ct);
        if (response.IsSuccessStatusCode)
        {
            return (true, "Indexing started.");
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, string.IsNullOrWhiteSpace(body) ? $"Error {(int)response.StatusCode}" : body);
    }

    public async Task<(bool Success, string Message)> UploadBookAsync(
        string bookId, string title, string author, string language, string edition,
        Stream fileStream, string fileName, CancellationToken ct = default)
    {
        await _client.EnsureAuthenticatedAsync(ct);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(bookId), "bookId");
        content.Add(new StringContent(title), "title");
        content.Add(new StringContent(author), "author");
        content.Add(new StringContent(language), "language");
        content.Add(new StringContent(edition), "edition");
        content.Add(new StreamContent(fileStream), "file", fileName);

        var response = await _client.Http.PostAsync("/api/admin/books/upload", content, ct);
        if (response.IsSuccessStatusCode)
        {
            return (true, "Book uploaded successfully.");
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, string.IsNullOrWhiteSpace(body) ? $"Upload failed ({(int)response.StatusCode})" : body);
    }
}
