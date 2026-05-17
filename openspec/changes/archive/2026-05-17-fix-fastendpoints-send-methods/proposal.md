## Why

All 10 FastEndpoints endpoints were using `HttpContext.Response.Send*` extension methods instead of the endpoint's own `Send.xxx` property methods. The extension methods bypass FastEndpoints' internal response-tracking flag, so the framework never marks the request as completed — causing requests to hang or continue executing after a response was intended to be sent.

## What Changes

- Replace all `HttpContext.Response.SendAsync(...)` calls with `Send.OkAsync(...)` or `Send.ResponseAsync(..., statusCode, ct)`
- Replace `HttpContext.Response.SendNotFoundAsync(ct)` with `Send.NotFoundAsync(ct)`
- Replace `HttpContext.Response.SendUnauthorizedAsync(ct)` with `Send.UnauthorizedAsync(ct)`
- Replace `HttpContext.Response.SendNoContentAsync(ct)` with `Send.NoContentAsync(ct)`
- Replace `HttpContext.Response.SendRedirectAsync(url, ...)` with `Send.RedirectAsync(url, ...)`
- Fix pre-existing bug in `TriggerIndexEndpoint`: `Send.OkAsync(..., statusCode: 202, ...)` used a non-existent named parameter; replaced with `Send.ResponseAsync(..., 202, ct)`

Affected endpoints (all in `MedAssist.Web/Endpoints/`):
- `Auth/LoginEndpoint.cs`
- `Auth/LogoutEndpoint.cs`
- `Books/ListBooksEndpoint.cs`
- `Books/TriggerIndexEndpoint.cs`
- `Dictionary/GetByIcdEndpoint.cs`
- `Dictionary/SearchDictionaryEndpoint.cs`
- `Query/QueryEndpoint.cs`
- `Users/CreateUserEndpoint.cs`
- `Users/DeleteUserEndpoint.cs`
- `Users/ListUsersEndpoint.cs`

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- None (implementation-only fix; no API contract or behavior changes)

## Impact

- **Code**: All endpoint `HandleAsync` methods in `MedAssist.Web/Endpoints/`
- **APIs**: No contract changes — status codes and response shapes are unchanged
- **Runtime**: Requests now properly terminate after `Send.xxx` calls, preventing hung connections and post-response execution
- **Build**: Fixed CS1739 compile error on `TriggerIndexEndpoint` (`statusCode` is not a valid named parameter on `OkAsync`)
