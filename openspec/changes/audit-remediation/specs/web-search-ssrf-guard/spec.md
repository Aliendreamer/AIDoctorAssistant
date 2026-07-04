## ADDED Requirements

### Requirement: Web fetch enforces the domain allowlist on the dereferenced URL

Before fetching a web-search result, the application SHALL validate the URL that is actually
dereferenced (not merely bias the search query). The URL SHALL use `https`, and its host SHALL
match a configured `AllowedDomains` entry by exact or dot-boundary suffix match. A URL failing
either check SHALL NOT be fetched.

#### Scenario: Non-allowlisted host is not fetched

- **WHEN** a search result URL's host is not in `AllowedDomains`
- **THEN** the application does not issue an HTTP request to it

### Requirement: Web fetch blocks internal and metadata addresses

The application SHALL resolve the target host and refuse to connect when any resolved address is
private, loopback, link-local, or unique-local (`10/8`, `172.16/12`, `192.168/16`, `127/8`,
`169.254/16`, `::1`, `fc00::/7`, `fe80::/10`). Redirects to a different host SHALL be rejected.

#### Scenario: Cloud metadata endpoint is blocked

- **WHEN** a result URL resolves to `169.254.169.254`
- **THEN** the fetch is refused

#### Scenario: Internal service name is blocked

- **WHEN** a result URL points at `http://qdrant:6334` or `http://ollama:11434`
- **THEN** the fetch is refused (scheme and/or allowlist and/or private-IP checks fail)
