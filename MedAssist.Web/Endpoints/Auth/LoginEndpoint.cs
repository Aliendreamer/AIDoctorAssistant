using FastEndpoints;
using FastEndpoints.Security;
using MedAssist.Shared.Models;
using MedAssist.Web.Data;
using System.Security.Claims;

namespace MedAssist.Web.Endpoints.Auth;

public sealed class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IConfiguration _configuration;
    private readonly UserRepository _users;

    public LoginEndpoint(IConfiguration configuration, UserRepository users)
    {
        _configuration = configuration;
        _users = users;
    }

    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await _users.FindByUsernameAsync(req.Username, ct);

        if (user is null || !_users.VerifyPassword(user, req.Password))
        {
            await HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        var secretKey = _configuration["Auth:Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Auth:Jwt:SecretKey is not configured");
        var issuer = _configuration["Auth:Jwt:Issuer"] ?? "medassist";
        var audience = _configuration["Auth:Jwt:Audience"] ?? "medassist-api";
        var expiryMinutes = int.TryParse(_configuration["Auth:Jwt:ExpiryMinutes"], out var m) ? m : 480;
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = JwtBearer.CreateToken(o =>
        {
            o.SigningKey = secretKey;
            o.Issuer = issuer;
            o.Audience = audience;
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
