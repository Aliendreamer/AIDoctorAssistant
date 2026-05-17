## Context

FastEndpoints provides two surfaces for sending responses from an endpoint:

1. **`HttpResponse` extension methods** (`HttpContext.Response.SendAsync`, `SendNotFoundAsync`, etc.) — low-level helpers on ASP.NET Core's `HttpResponse`. They write bytes to the response stream but do **not** set FastEndpoints' internal `ResponseSent` flag.
2. **`Send.*` property methods** (`Send.OkAsync`, `Send.NotFoundAsync`, etc.) on the endpoint base class — these go through `ResponseSender<TReq, TRes>`, which sets the `ResponseSent` flag and properly terminates the middleware pipeline.

All 10 endpoints were using surface #1, which means FastEndpoints did not know the response had been sent, so `HandleAsync` could continue executing after the response was written (or hang depending on middleware ordering).

## Goals / Non-Goals

**Goals:**
- Replace every `HttpContext.Response.Send*` call with the equivalent `Send.*` method
- Fix the pre-existing `Send.OkAsync(statusCode: 202)` compile error in `TriggerIndexEndpoint`
- Zero behavior change from the caller's perspective (same status codes, same response shapes)

**Non-Goals:**
- Changing response schemas or status codes
- Refactoring endpoint logic beyond the send calls
- Adding validation or error handling beyond what existed

## Decisions

**`Send.ResponseAsync(body, statusCode, ct)` for non-200 responses**
`Send.OkAsync` is hardcoded to 200. For 400/201/409/202 responses, `Send.ResponseAsync(response, statusCode, ct)` is the correct overload on `ResponseSender`. `Send.CreatedAtAsync` was not used because the endpoints don't route to a resource URL.

**`Send.RedirectAsync(url, isPermanent)` for LogoutEndpoint**
`HttpContext.SignOutAsync` is kept (it is a cookie auth operation, not a response send) and only the redirect call is switched to `Send.RedirectAsync`.

**No `SendStringAsync` for string error bodies**
`Send.ResponseAsync(string, statusCode, ct)` serializes the string as a JSON string (`"..."`) which is consistent with what `HttpContext.Response.SendAsync(string, statusCode)` was doing. Switching to plain-text would be a behavior change.

## Risks / Trade-offs

- **[Risk] LSP showed a phantom 'S' diagnostic on TriggerIndexEndpoint line 104** → `dotnet build` confirmed zero errors; diagnostic was a stale LSP artifact.
- **[Trade-off] Anonymous-type bodies in `Send.ResponseAsync`** → `TResponse` resolves to `object` on `EndpointWithoutRequest`; anonymous types are assignable to `object` so serialization works identically.
