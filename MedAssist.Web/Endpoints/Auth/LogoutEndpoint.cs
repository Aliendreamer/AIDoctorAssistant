using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MedAssist.Web.Endpoints.Auth;

public sealed class LogoutEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        // POST (not GET) so a cross-site <img>/link/prefetch can't trigger a logout (audit P3-5).
        Post("/logout");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await Send.RedirectAsync("/login", isPermanent: false);
    }
}
