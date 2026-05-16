namespace MedAssist.Shared.Models;

public sealed class LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public string Role { get; init; } = string.Empty;
}
