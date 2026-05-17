## 1. Auth Endpoints

- [x] 1.1 LoginEndpoint: replace `HttpContext.Response.SendUnauthorizedAsync` → `Send.UnauthorizedAsync`
- [x] 1.2 LogoutEndpoint: replace `HttpContext.Response.SendRedirectAsync` → `Send.RedirectAsync`

## 2. Books Endpoints

- [x] 2.1 ListBooksEndpoint: replace `HttpContext.Response.SendAsync` → `Send.OkAsync`
- [x] 2.2 TriggerIndexEndpoint: replace `HttpContext.Response.SendAsync` (400) → `Send.ResponseAsync(..., 400, ct)`
- [x] 2.3 TriggerIndexEndpoint: replace `HttpContext.Response.SendNotFoundAsync` → `Send.NotFoundAsync`
- [x] 2.4 TriggerIndexEndpoint: replace `HttpContext.Response.SendAsync` (409) → `Send.ResponseAsync(..., 409, ct)`
- [x] 2.5 TriggerIndexEndpoint: fix `Send.OkAsync(..., statusCode: 202, ...)` → `Send.ResponseAsync(..., 202, ct)`

## 3. Dictionary Endpoints

- [x] 3.1 GetByIcdEndpoint: replace `HttpContext.Response.SendNotFoundAsync` → `Send.NotFoundAsync`
- [x] 3.2 GetByIcdEndpoint: replace `HttpContext.Response.SendAsync` → `Send.OkAsync`
- [x] 3.3 SearchDictionaryEndpoint: replace `HttpContext.Response.SendAsync` → `Send.OkAsync`

## 4. Query Endpoint

- [x] 4.1 QueryEndpoint: replace `HttpContext.Response.SendAsync` → `Send.OkAsync`

## 5. User Endpoints

- [x] 5.1 CreateUserEndpoint: replace `HttpContext.Response.SendAsync` (400 password) → `Send.ResponseAsync`
- [x] 5.2 CreateUserEndpoint: replace `HttpContext.Response.SendAsync` (400 role) → `Send.ResponseAsync`
- [x] 5.3 CreateUserEndpoint: replace `HttpContext.Response.SendAsync` (201) → `Send.ResponseAsync`
- [x] 5.4 CreateUserEndpoint: replace `HttpContext.Response.SendAsync` (409) → `Send.ResponseAsync`
- [x] 5.5 DeleteUserEndpoint: replace `HttpContext.Response.SendAsync` (400) → `Send.ResponseAsync`
- [x] 5.6 DeleteUserEndpoint: replace `HttpContext.Response.SendNotFoundAsync` → `Send.NotFoundAsync`
- [x] 5.7 DeleteUserEndpoint: replace `HttpContext.Response.SendAsync` (409) → `Send.ResponseAsync`
- [x] 5.8 DeleteUserEndpoint: replace `HttpContext.Response.SendNoContentAsync` → `Send.NoContentAsync`
- [x] 5.9 ListUsersEndpoint: replace `HttpContext.Response.SendAsync` → `Send.OkAsync`

## 6. Verification

- [x] 6.1 Run `dotnet build` — confirm 0 errors, 0 warnings
