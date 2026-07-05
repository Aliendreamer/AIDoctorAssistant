## ADDED Requirements

### Requirement: JWT signing key is externally supplied and validated at startup

The JWT signing key SHALL be supplied from a secret source (environment variable or secret store),
not from committed configuration. In the Production environment, startup SHALL fail fast if the key
is the known placeholder value or is shorter than 32 bytes.

#### Scenario: Placeholder key refuses to boot in Production

- **WHEN** the app starts in Production with the signing key equal to the committed placeholder
- **THEN** startup fails with a clear error and the app does not serve requests

#### Scenario: Valid key boots

- **WHEN** a random key of at least 32 bytes is supplied
- **THEN** the app starts and issues/validates tokens with it

### Requirement: Admin credential is not a committed default

The first-run admin account SHALL be created with a strong secret supplied out-of-band (or a
generated random password), not a plaintext password committed to configuration. Seeding SHALL be
idempotent and SHALL NOT reset an existing admin.

#### Scenario: No plaintext admin password in committed config

- **WHEN** the repository configuration is inspected
- **THEN** it contains no usable admin/doctor plaintext password

### Requirement: Secrets are not baked into the container image

The application image SHALL NOT copy the decrypted `config/` (or `books/`) into image layers.
Configuration containing secrets SHALL be mounted at runtime.

#### Scenario: Image inspection reveals no secrets

- **WHEN** the built image layers/history are inspected
- **THEN** no JWT secret, user password, or database password is present

### Requirement: Transport is secured and the auth cookie is protected

The application SHALL enforce HTTPS (redirect and HSTS meaningful behind TLS), and the Blazor auth
cookie SHALL be issued with `Secure=Always`, `HttpOnly=true`, and `SameSite=Lax`.

#### Scenario: Auth cookie carries security attributes

- **WHEN** a user authenticates through the Blazor UI
- **THEN** the issued cookie has the `Secure`, `HttpOnly`, and `SameSite=Lax` attributes
