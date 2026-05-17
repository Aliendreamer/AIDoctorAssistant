## ADDED Requirements

### Requirement: Endpoints use Send property for all responses
All FastEndpoints endpoint `HandleAsync` implementations SHALL use the `Send.*` property methods
(e.g. `Send.OkAsync`, `Send.NotFoundAsync`, `Send.ResponseAsync`) for every response path,
never `HttpContext.Response.Send*` extension methods. This ensures FastEndpoints' internal
`ResponseSent` flag is set, which properly terminates the request pipeline.

#### Scenario: Successful response terminates request
- **WHEN** an endpoint calls any `Send.*` method
- **THEN** the request pipeline is marked as complete and no further response writes occur

#### Scenario: Error response terminates request
- **WHEN** an endpoint calls `Send.NotFoundAsync`, `Send.UnauthorizedAsync`, or `Send.ResponseAsync` with a non-2xx status
- **THEN** execution returns after the `await` and the pipeline is terminated

#### Scenario: Redirect terminates request
- **WHEN** `LogoutEndpoint` calls `Send.RedirectAsync`
- **THEN** the client receives the redirect and the pipeline is terminated

### Requirement: Non-200 status codes use Send.ResponseAsync
Endpoints that need to return a status code other than 200 SHALL use
`Send.ResponseAsync(body, statusCode, ct)`. `Send.OkAsync` SHALL NOT be called
with a `statusCode` named parameter (no such overload exists).

#### Scenario: 202 Accepted response
- **WHEN** `TriggerIndexEndpoint` starts background indexing
- **THEN** it responds with `Send.ResponseAsync(body, 202, ct)`, not `Send.OkAsync`

#### Scenario: 400/409 error responses
- **WHEN** an endpoint detects a validation or conflict error
- **THEN** it uses `Send.ResponseAsync(message, 400|409, ct)` to send the error body with the correct status
