using MedAssist.Shared.Models;

namespace MedAssist.Web.Services;

public sealed class AdminApiClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminApiClient> _logger;
    private string? _bearerToken;

    public AdminApiClient(HttpClient http, IConfiguration configuration, ILogger<AdminApiClient> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;

        var baseUrl = configuration["WebApp:SelfBaseUrl"] ?? "http://localhost:8080";
        _http.BaseAddress = new Uri(baseUrl);
    }

    public HttpClient Http => _http;

    public async Task EnsureAuthenticatedAsync(CancellationToken ct = default)
    {
        if (_bearerToken is not null)
        {
            return;
        }

        var users = _configuration.GetSection("Auth:Users").Get<AdminCredential[]>() ?? [];
        var admin = Array.Find(users, u => string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase));
        if (admin is null)
        {
            _logger.LogError("No Admin user found in Auth:Users config for AdminApiClient");
            return;
        }

        var loginResponse = await _http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = admin.Username, Password = admin.Password }, ct);

        if (!loginResponse.IsSuccessStatusCode)
        {
            _logger.LogError("AdminApiClient failed to obtain JWT: {Status}", loginResponse.StatusCode);
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
