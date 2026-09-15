# AvaEntra

Local Entra ID–compatible identity provider for testing SPAs and APIs without Azure.

It speaks the Microsoft identity platform v2 protocol: OIDC discovery, authorization code + PKCE, refresh tokens, client credentials, resource-owner password (for scripts), and on-behalf-of (OBO). Tokens use Entra-style claims (`oid`, `tid`, `scp`, `roles`, `groups`, `ver`).

The React admin UI manages users, groups, app registrations, secrets, scopes, and roles.

## Quick start

Terminal 1 — identity server:

```bash
dotnet run --project server
```

Terminal 2 — admin UI (hot reload):

```bash
cd admin
npm install
npm run dev
```

- Admin UI: [http://localhost:5173](http://localhost:5173)
- Identity host: [http://localhost:5100](http://localhost:5100)

To serve the admin UI from the same host as identity:

```bash
cd admin && npm run build
dotnet run --project server
```

Then open [http://localhost:5100](http://localhost:5100).

This is a local development tool. Do not expose it to the internet.

## Docker

The image restores NuGet packages from **nuget.org** (the local `nuget.config` is not used). CI builds and pushes to GitHub Container Registry on `main` / version tags.

```bash
docker build -t avaentra .
docker run --rm -p 5100:8080 \
  -e AvaEntra__PublicOrigin=http://localhost:5100 \
  -e AvaEntra__SeedUserPassword=Passw0rd! \
  -e AvaEntra__SeedBackendSecret=dev-backend-secret \
  -e AvaEntra__AdminUsername=admin \
  -e AvaEntra__AdminPassword=AdminPassw0rd! \
  -v avaentra-data:/app/storage \
  avaentra
```

Then open [http://localhost:5100](http://localhost:5100). Set `AvaEntra__PublicOrigin` to the URL your apps use to reach the container so issuers and discovery match.

Published images (after a push to `main`):

```bash
docker pull ghcr.io/<owner>/avaentra:latest
```

## Seed directory

Created on first run in `server/storage/`. Delete that folder to reset.

Credentials from config / environment:

| Variable | Default | Purpose |
| --- | --- | --- |
| `AvaEntra__AdminUsername` | `admin` | Management UI username |
| `AvaEntra__AdminPassword` | `AdminPassw0rd!` | Management UI password |
| `AvaEntra__SeedUserPassword` | `Passw0rd!` | Password for seeded directory users (first run only) |
| `AvaEntra__SeedBackendSecret` | `dev-backend-secret` | Client secret for Sample Backend (first run only) |

| Item | Value |
| --- | --- |
| Tenant ID | `11111111-1111-1111-1111-111111111111` |
| Users | `admin@avaentra.local`, `alice@avaentra.local`, `bob@avaentra.local` |
| SPA client ID | `55555555-5555-5555-5555-555555555555` |
| API audience | `api://sample-api` |
| API scope | `api://sample-api/access_as_user` |
| Backend client ID | `77777777-7777-7777-7777-777777777777` |

The management UI requires admin login. The IdP login page (for SPAs) lists seed directory users for one-click sign-in.

## Point your apps at AvaEntra

Authority:

```text
http://localhost:5100/11111111-1111-1111-1111-111111111111
```

### SPA (MSAL.js + PKCE)

Register your redirect URI on the Sample SPA (or a new public client) in the admin UI.

```js
const msalConfig = {
  auth: {
    clientId: "55555555-5555-5555-5555-555555555555",
    authority: "http://localhost:5100/11111111-1111-1111-1111-111111111111",
    knownAuthorities: ["localhost:5100"],
    redirectUri: "http://localhost:3000"
  }
};

const loginRequest = {
  scopes: ["openid", "profile", "offline_access", "api://sample-api/access_as_user"]
};
```

### API (Microsoft.Identity.Web)

```json
"AzureAd": {
  "Instance": "http://localhost:5100/",
  "TenantId": "11111111-1111-1111-1111-111111111111",
  "ClientId": "66666666-6666-6666-6666-666666666666",
  "Audience": "api://sample-api"
}
```

Allow HTTP metadata in development:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.Configure<JwtBearerOptions>(
    JwtBearerDefaults.AuthenticationScheme,
    options => options.RequireHttpsMetadata = false);
```

### On-behalf-of (middle-tier API → downstream API)

```http
POST /{tenant}/oauth2/v2.0/token
Content-Type: application/x-www-form-urlencoded

grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer
client_id=77777777-7777-7777-7777-777777777777
client_secret=dev-backend-secret
assertion=<user access token received by the middle tier>
requested_token_use=on_behalf_of
scope=api://sample-api/access_as_user
```

The Sample Backend app has OBO and client credentials enabled. Enable **On-behalf-of** on any confidential client that needs this flow.

### Resource owner password (scripts / tests)

```bash
curl -s http://localhost:5100/11111111-1111-1111-1111-111111111111/oauth2/v2.0/token \
  -d grant_type=password \
  -d client_id=55555555-5555-5555-5555-555555555555 \
  -d username=alice@avaentra.local \
  -d password=Passw0rd! \
  -d scope="openid profile offline_access api://sample-api/access_as_user"
```

## Protocol surface

| Endpoint | Purpose |
| --- | --- |
| `/{tenant}/v2.0/.well-known/openid-configuration` | OIDC discovery (MSAL / Identity.Web) |
| `/{tenant}/discovery/v2.0/keys` | JWKS |
| `/{tenant}/oauth2/v2.0/authorize` | Authorization code + PKCE |
| `/{tenant}/oauth2/v2.0/token` | Code, refresh, client credentials, password, OBO |
| `/{tenant}/oauth2/v2.0/logout` | End session |
| `/v1.0/me`, `/v1.0/me/memberOf` | Graph-lite profile |

`tenant` can be the tenant GUID, `avaentra.local`, or `common`.

Access tokens include `oid`, `tid`, `preferred_username`, `scp`, `roles`, `groups`, `azp`, and `ver=2.0`. Audience is the API identifier URI (`api://sample-api`).

## Admin UI

- **Users** — create, edit, disable, reset password, group membership, app-role assignment
- **Groups** — create/edit and manage members
- **App registrations** — SPA / web / API / confidential clients, redirect URIs, client secrets, exposed scopes, app roles, PKCE / OBO / client-credentials flags
- **Sign-in logs** — recent token issuances

Directory data is stored as JSON in `server/storage/directory.json`. The signing key is in `server/storage/signing-key.json`.
