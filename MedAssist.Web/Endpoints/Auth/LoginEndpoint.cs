using FastEndpoints;
using FastEndpoints.Security;
using MedAssist.Shared.Models;
using System.Security.Claims;

namespace MedAssist.Web.Endpoints.Auth;

public sealed class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IConfiguration _configuration;

    public LoginEndpoint(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var users = _configuration.GetSection("Auth:Users").Get<UserCredential[]>() ?? [];
        var user = Array.Find(users, u =>
            string.Equals(u.Username, req.Username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == req.Password);

        if (user is null)
        {
            await HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var secretKey = _configuration["Auth:Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Auth:Jwt:SecretKey is not configured");
        var expiryMinutes = int.TryParse(_configuration["Auth:Jwt:ExpiryMinutes"], out var m) ? m : 480;
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = JwtBearer.CreateToken(o =>
        {
            o.SigningKey = secretKey;
            o.ExpireAt = expiresAt;
            o.User.Roles.Add(user.Role);
            o.User.Claims.Add(new Claim(ClaimTypes.Name, user.Username));
        });

        await HttpContext.Response.SendAsync(new LoginResponse
        {
            Token = token,
            ExpiresAt = new DateTimeOffset(expiresAt, TimeSpan.Zero),
            Role = user.Role
        }, cancellation: ct);
    }
}

internal sealed class UserCredential
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
}
