using MedAssist.Shared.Models;

namespace MedAssist.Web.Services;

public sealed class AdminBookService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminBookService> _logger;
    private string? _bearerToken;

    public AdminBookService(HttpClient http, IConfiguration configuration, ILogger<AdminBookService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = configuration["WebApp:SelfBaseUrl"] ?? "http://localhost:8080";
        _http.BaseAddress = new Uri(baseUrl);
    }

    public async Task<IReadOnlyList<BookInfo>> GetBooksAsync(CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.GetFromJsonAsync<List<BookInfo>>("/api/books", ct);
        return response ?? [];
    }

    public async Task<(bool Success, string Message)> TriggerReindexAsync(int bookId, CancellationToken ct = default)
    {
        await EnsureTokenAsync(ct);
        var response = await _http.PostAsync($"/api/admin/index?id={bookId}&force=true", content: null, ct);
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
        await EnsureTokenAsync(ct);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(bookId), "bookId");
        content.Add(new StringContent(title), "title");
        content.Add(new StringContent(author), "author");
        content.Add(new StringContent(language), "language");
        content.Add(new StringContent(edition), "edition");
        content.Add(new StreamContent(fileStream), "file", fileName);

        var response = await _http.PostAsync("/api/admin/books/upload", content, ct);
        if (response.IsSuccessStatusCode)
        {
            return (true, "Book uploaded successfully.");
        }
        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, string.IsNullOrWhiteSpace(body) ? $"Upload failed ({(int)response.StatusCode})" : body);
    }

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (_bearerToken is not null)
        {
            return;
        }

        var users = _configuration.GetSection("Auth:Users").Get<AdminCredential[]>() ?? [];
        var admin = Array.Find(users, u => string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase));
        if (admin is null)
        {
            _logger.LogError("No Admin user found in Auth:Users config for AdminBookService");
            return;
        }

        var loginResponse = await _http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = admin.Username, Password = admin.Password }, ct);

        if (!loginResponse.IsSuccessStatusCode)
        {
            _logger.LogError("AdminBookService failed to obtain JWT: {Status}", loginResponse.StatusCode);
            return;
        }

        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ct);
        if (result is not null)
        {
            _bearerToken = result.Token;
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _bearerToken);
        }
    }

    private sealed class AdminCredential
    {
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
    }
}
