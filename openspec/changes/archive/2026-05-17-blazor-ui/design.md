## Context

The web host (`MedAssist.Web`) runs both FastEndpoints REST endpoints and a Blazor Server app in the same ASP.NET Core process. Auth is currently JWT-only via FastEndpoints' `AddAuthenticationJwtBearer`, which registers JWT as the default authentication scheme. Blazor interactive server components cannot use JWT bearer tokens (no `Authorization` header in WebSocket connections), so Blazor pages need a separate cookie scheme.

Config-based user list lives in `Auth:Users` in `appsettings.shared.json` — `admin`/`doctor` credentials with role strings. The Login endpoint (`POST /api/auth/login`) already validates against this list and returns a JWT. The Blazor login page will reuse the same validation logic but issue a cookie instead.

## Goals / Non-Goals

**Goals:**
- Add cookie auth scheme alongside JWT so Blazor pages can authenticate
- Login page at `/login` that signs in with a cookie, logout that clears it
- Public area (`/query`) accessible to Doctor and Admin roles
- Admin area (`/admin/books`, `/admin/books/upload`) restricted to Admin role
- Delete the scaffold Counter/Weather/Home pages; replace Home with redirect to `/query`

**Non-Goals:**
- No changes to the REST API auth (stays JWT)
- No persistent user store — config list only
- No role management UI
- No streaming SSE in the Blazor query page (plain async call, no chunked response)

## Decisions

### D1: Two auth schemes, scheme-per-path routing

FastEndpoints sets `CookieOrJwtBearerTokens` or `JwtBearer` as default. We add `AddCookie` alongside it. For API endpoints FastEndpoints picks JWT (it specifies the scheme explicitly via `[Authorize(AuthenticationSchemes="Bearer")]` or the global option). For Blazor pages, the cookie scheme is used by `AuthorizeRouteView`.

To avoid scheme conflict we register cookie auth with name `"Cookies"` and leave JWT as the primary default scheme for API endpoints. The Blazor `AuthorizeRouteView` doesn't specify a scheme — it uses whichever the runtime finds for the current user principal, which for browser requests will be the cookie.

**Alternative considered**: A single unified scheme. Rejected — would require converting API clients to also send cookies, or accepting JWTs in Blazor via JS interop. Both add complexity for no gain.

### D2: Login page is a plain Blazor page, no server-side redirect

`/login` is an `@page` component with a form. On submit it calls `HttpContext.SignInAsync("Cookies", ...)` via a `[CascadingParameter] HttpContext HttpContext` (server-side pre-render). After sign-in, it does a full page redirect via `NavigationManager.NavigateTo("/query", forceLoad: true)` to ensure the cookie is sent in the next request.

**Alternative considered**: Razor Page for login (outside Blazor). Works but adds a second page model stack to maintain. Blazor component is simpler given everything else is Blazor.

### D3: Single `AuthorizeRouteView` with per-page role attributes

`Routes.razor` switches from `RouteView` to `AuthorizeRouteView`. Each page declares `@attribute [Authorize]` or `@attribute [Authorize(Roles="Admin")]`. The `NotAuthorizedContent` template in `AuthorizeRouteView` redirects to `/login` via `NavigationManager`.

**Alternative considered**: Middleware-level redirect. Doesn't apply cleanly to Blazor interactive routes since they go through the WebSocket channel after initial load.

### D4: `AdminBookService` calls admin REST endpoints via `HttpClient`

Admin pages need to list books, trigger re-index, and upload PDFs. These operations already have REST endpoints (`GET /api/admin/books`, `POST /api/admin/books/upload`, `POST /api/admin/books/{id}/index`). Rather than duplicating DB access in Blazor components, `AdminBookService` is a scoped service that calls these endpoints. Since Blazor Server is in-process, the `HttpClient` targets `localhost` with the same port — no network hop.

**Problem**: The admin endpoints require a JWT bearer token, but the Blazor session has a cookie. The `AdminBookService` needs to authenticate itself.

**Resolution**: `AdminBookService` calls `POST /api/auth/login` with the admin credentials from config to obtain a JWT, caches it for the session lifetime, and attaches it as `Authorization: Bearer ...`. The admin credentials are read directly from `IConfiguration` — no secrets stored in user state.

**Alternative considered**: Relax admin endpoint auth to accept cookies too. Rejected — changes REST API auth behavior and adds dual-scheme complexity at the endpoint level.

### D5: Two layouts — `MainLayout` and `AdminLayout`

Public pages use the existing `MainLayout` (cleaned up with MedAssist brand, Query link, Logout). Admin pages use a new `AdminLayout` that inherits the same shell but adds an admin sidebar nav (Books, Upload). Role-conditional rendering hides admin links from Doctor-role users if they somehow reach the nav.

## Risks / Trade-offs

- **`HttpContext` in Blazor**: `CascadingParameter HttpContext` is only available during pre-render (SSR). After the WebSocket connection upgrades, `HttpContext` is null. Sign-in/sign-out must happen before the interactive render phase. The login form uses `enhance="true"` POST (Enhanced Navigation) to trigger sign-in in the SSR pass. → Mitigation: Use `@rendermode InteractiveServer` only on pages that don't need `HttpContext`; Login page stays SSR-only.

- **Admin JWT self-call**: If admin credentials change in config, the cached token becomes stale until the app restarts. → Acceptable for this use case — config user list is not intended to change at runtime.

- **Circular HTTP call**: `AdminBookService` calls `localhost:{port}/api/auth/login`. In Docker this loops through the same container. → No issue functionally, but adds one extra HTTP round-trip per session start. Token is cached in the scoped service lifetime (per-circuit), so it's a one-time cost.

## Migration Plan

1. Add `Microsoft.AspNetCore.Authentication.Cookies` package (already in ASP.NET Core SDK, no extra NuGet needed — just call `.AddCookie()`)
2. Update `AddAuth` extension to chain `.AddCookie()` after `AddAuthenticationJwtBearer`
3. Update `App.razor` with `CascadingAuthenticationState`
4. Update `Routes.razor` with `AuthorizeRouteView`
5. Add Login, Admin/Books, Admin/UploadBook pages
6. Polish Query.razor
7. Clean up nav, delete scaffold pages
8. Add `AdminBookService`
9. No DB migration needed
