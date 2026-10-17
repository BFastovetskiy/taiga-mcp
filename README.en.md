# Taiga MCP Server — MCP server for the Taiga REST API

**English** | [Русский](README.md)

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![MCP Server](https://img.shields.io/badge/MCP%20Server-Active-green.svg)](https://modelcontextprotocol.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

## Project Description

**Taiga MCP Server** is a Model Context Protocol (MCP) server written in C#/.NET 8.0 that gives LLM agents access to the **Taiga** REST API over HTTP. It exposes 72 tools for managing projects, user stories, tasks, issues, epics, roles, users, and project members/invitations.

- **Transport:** Streamable HTTP (`ModelContextProtocol.AspNetCore`).
- **Platform:** .NET 8.0.
- **Sources:** built solely on the official C# MCP SDK and the Taiga REST API (v1).

## Key Features

- **72 MCP tools** — Projects, User Stories, Tasks, Issues, Epics, Roles, Users, Memberships/Invitations.
- **Taiga authentication** — username/password login, a ready-made token, a refresh token, and a cookie session (for SSO).
- **Optimistic concurrency control (OCC)** — the `version` parameter when updating objects.
- **Pagination control** — the `x-disable-pagination` header.
- **Resilience** — automatic retries on 408/429/5xx and network failures with exponential backoff.
- **Corporate/self-signed certificates** — certificate pinning or a trusted root CA.
- **MCP endpoint protection** — a shared token (`X-Api-Key`/`Bearer`), compared in constant time.
- **Configurable listen address and port** (the `Server` section).
- **Health endpoints** — `/health`, `/health/live`, `/health/ready`.
- **Audit log** of destructive tool calls (ILogger + JSON Lines file with rotation).
- **Rate limiting** for the MCP endpoint (key: token or client IP).
- **Prometheus metrics** (`/metrics`) — RPS, latency, and error rate of tool calls.
- **Graceful shutdown** and shutdown on critical errors.
- **Configuration via ENV** — any setting can be overridden by an environment variable.
- **Docker** — a multi-stage `Dockerfile`: app in `/opt/mcp`, runs as non-root (`app`, UID 1654), parameters via ENV, config and certificates via mounts.
- **Unit tests** (xUnit).

## Requirements

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) or newer (the project targets `net8.0`).
- Access to a Taiga instance over its REST API (`/api/v1`).

## Quick Start

```powershell
# Build
dotnet build .\TaigaMcp.csproj -c Release

# Run (listens on 0.0.0.0:5000 by default)
dotnet run --project .\TaigaMcp.csproj

# Unit tests
dotnet test .\tests\TaigaMcp.Tests\TaigaMcp.Tests.csproj
```

Health check:

```powershell
curl http://127.0.0.1:5000/health         # -> ok (general)
curl http://127.0.0.1:5000/health/live    # -> ok (process alive)
curl http://127.0.0.1:5000/health/ready   # -> {"status":"Healthy"} (Taiga API reachable)
```

The MCP endpoint is the application root: `http://127.0.0.1:5000/`.

## Architecture

```text
TaigaMcp.csproj              # net8.0 project + MCP SDK packages
Program.cs                   # Entry point: configuration, DI, HTTP transport, endpoint protection
appsettings.json             # Base settings (no secrets)
Configuration/
  TaigaOptions.cs            # Connection settings model
  ServerOptions.cs           # Listen address and port
  AuditOptions.cs            # Audit log settings
  MetricsOptions.cs          # Prometheus metrics settings
  RateLimitOptions.cs        # Rate limiting settings
  ShutdownOptions.cs         # Graceful shutdown settings
  HealthOptions.cs           # Health/readiness settings
Health/
  HealthChecks.cs            # Readiness/liveness checks
  HealthEndpoints.cs         # Registers /health, /health/live, /health/ready
  ReadinessProbe.cs          # Caching readiness probe (single-flight)
Hosting/
  GracefulShutdown.cs        # Graceful shutdown and critical-error handling
Logging/
  AuditLogger.cs             # Audit log of destructive tools (+ rotation)
Metrics/
  McpMetrics.cs              # Prometheus metrics (RPS, latency, error rate)
  ToolNameResolver.cs        # Maps a method to the tool name
Security/
  RateLimitSetup.cs          # Rate limiting (partition by token/IP)
Taiga/
  TaigaClient.cs             # REST API wrapper: auth, retries, pagination, formatting
  TaigaAuthStore.cs          # Singleton token cache
  TaigaHttpHandlerFactory.cs # TLS handling (pinning / trusted CA / disabled validation)
  TaigaApiException.cs       # Taiga API errors
  JsonBody.cs                # JSON request body builder
Tools/
  TaigaToolBase.cs           # Base tool class (+ audit, metrics)
  *Tools.cs                  # 8 groups of MCP tools
certs/                       # Certificates (e.g. for pinning)
tests/
  TaigaMcp.Tests/            # Unit tests (xUnit)
Dockerfile                   # Image: /opt/mcp, runs as non-root (app)
.dockerignore                # What stays out of the build context (secrets, certs, tests)
global.json                  # Pins the .NET SDK version
```

## Tools (72)

### Projects (10)

`taiga_list_projects`, `taiga_get_project`, `taiga_get_project_by_slug`, `taiga_create_project`,
`taiga_update_project`, `taiga_delete_project`, `taiga_get_project_stats`, `taiga_leave_project`,
`taiga_like_project`, `taiga_unlike_project`

### User Stories (11)

`taiga_list_userstories`, `taiga_get_userstory`, `taiga_get_userstory_by_ref`, `taiga_create_userstory`,
`taiga_update_userstory`, `taiga_delete_userstory`, `taiga_bulk_create_userstories`, `taiga_vote_userstory`,
`taiga_unvote_userstory`, `taiga_watch_userstory`, `taiga_unwatch_userstory`

### Tasks (9)

`taiga_list_tasks`, `taiga_get_task`, `taiga_get_task_by_ref`, `taiga_create_task`, `taiga_update_task`,
`taiga_delete_task`, `taiga_bulk_create_tasks`, `taiga_watch_task`, `taiga_unwatch_task`

### Issues (8)

`taiga_list_issues`, `taiga_get_issue`, `taiga_get_issue_by_ref`, `taiga_create_issue`, `taiga_update_issue`,
`taiga_delete_issue`, `taiga_watch_issue`, `taiga_unwatch_issue`

### Epics (13)

`taiga_list_epics`, `taiga_get_epic`, `taiga_get_epic_by_ref`, `taiga_create_epic`, `taiga_update_epic`,
`taiga_delete_epic`, `taiga_bulk_create_epics`, `taiga_list_epic_related_userstories`,
`taiga_create_epic_related_userstory`, `taiga_delete_epic_related_userstory`, `taiga_vote_epic`,
`taiga_watch_epic`, `taiga_unwatch_epic`

### Roles (5)

`taiga_list_roles`, `taiga_get_role`, `taiga_create_role`, `taiga_update_role`, `taiga_delete_role`

### Users (8)

`taiga_list_users`, `taiga_get_user`, `taiga_get_current_user`, `taiga_register_user`, `taiga_update_user`,
`taiga_delete_user`, `taiga_list_user_contacts`, `taiga_request_password_recovery`

### Memberships / Invitations (8)

`taiga_list_memberships`, `taiga_get_membership`, `taiga_create_membership`, `taiga_bulk_create_memberships`,
`taiga_update_membership`, `taiga_delete_membership`, `taiga_resend_invitation`, `taiga_get_invitation`

## Configuration

Settings are read in increasing order of precedence:

1. `appsettings.json` — base values (no secrets);
2. `appsettings.Local.json` — optional local file with secrets (listed in `.gitignore`);
3. environment variables.

### `Server` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `Host` | `0.0.0.0` | Listen address: `0.0.0.0` for all interfaces, `127.0.0.1`/`localhost` for local only, or a specific IP. |
| `Port` | `5000` | Listen port. |

### `Taiga` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `BaseUrl` | `https://api.taiga.io/api/v1` | Taiga API base URL. |
| `Username` | — | Username or email. |
| `Password` | — | Password. |
| `Token` | — | Ready-made `auth_token` (SSO-friendly option). |
| `RefreshToken` | — | Refresh token. |
| `AuthMethod` | `auto` | `auto` \| `password` \| `token` \| `cookie` (see below). |
| `AuthScheme` | `Bearer` | `Authorization` header scheme (`Bearer` or `Application`). |
| `Cookie` | — | Value of the `Cookie` header for the `cookie` mode. |
| `CaCertificatePath` | — | Path to a certificate (pinning / root CA). A relative path resolves against the assembly directory. |
| `IgnoreSslErrors` | `false` | Fully disable TLS certificate validation (last resort). |
| `DisablePaginationByDefault` | `true` | Disable pagination (the `x-disable-pagination` header). |
| `TimeoutSeconds` | `30` | Taiga HTTP request timeout (sec). |
| `MaxRetries` | `3` | Number of retries on transient failures. |
| `RetryBaseDelayMs` | `500` | Base retry delay (ms), grows exponentially. |

### `Mcp` section

| Key | Description |
| ----- | ------------- |
| `AuthToken` | Shared token that guards the MCP endpoint. If empty, the endpoint is unprotected (local use only). |

### `Audit` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `Enabled` | `true` | Whether the audit log of destructive tools is enabled. |
| `FilePath` | `logs/audit.log` | Audit log file (JSON Lines). If empty, entries go to ILogger only (category `TaigaMcp.Audit`). A relative path is resolved from the build directory. |
| `MaxFileSizeMB` | `64` | Max audit file size in MB before rotation. `0` disables rotation. |
| `MaxBackupFiles` | `5` | Number of rotated backups (`audit.log.1` … `audit.log.N`); the oldest is deleted. |

### `Metrics` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `Enabled` | `true` | Whether Prometheus metrics collection and export is enabled. |
| `Path` | `/metrics` | Path that serves the metrics. The endpoint is not protected by the MCP token. |

Exported tool-call metrics:

| Metric | Type | Purpose |
| -------- | ------ | --------- |
| `taiga_mcp_tool_requests_total{tool,status}` | counter | RPS (total tool calls). |
| `taiga_mcp_tool_errors_total{tool}` | counter | Error rate (number of errors). |
| `taiga_mcp_tool_request_duration_seconds{tool}` | histogram | Latency (call duration). |

`UseHttpMetrics` additionally exposes standard HTTP metrics (`http_requests_received_total`, `http_request_duration_seconds`, ...).

### `RateLimit` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `Enabled` | `true` | Whether request rate limiting is enabled. |
| `PermitLimit` | `120` | Max requests per window per key (token or IP). |
| `WindowSeconds` | `60` | Window size in seconds. |
| `QueueLimit` | `0` | Queue length beyond the limit; `0` means excess requests are rejected immediately (429). |

The partition key is the access token (stable hash) or the client IP. Health and metrics endpoints are exempt. When the limit is exceeded, `429 Too Many Requests` is returned with a `Retry-After` header.

### `Shutdown` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `ShutdownTimeoutSeconds` | `30` | How long to wait for in-flight requests to finish on shutdown. |
| `ShutdownOnUnhandledException` | `true` | Trigger a graceful shutdown on an unhandled exception. |

### `Health` section

| Key | Default | Description |
| ----- | --------- | ------------- |
| `ReadyCacheSeconds` | `60` | Cache interval in seconds for the `/health/ready` result. The real request to Taiga runs at most once per interval; `0` checks on every request. |

### Environment variables

They override the corresponding settings:

| Variable | Config equivalent |
| ---------- | ------------------- |
| `MCP_HOST` | `Server:Host` |
| `MCP_PORT` | `Server:Port` |
| `MCP_AUTH_TOKEN` | `Mcp:AuthToken` |
| `AUDIT_ENABLED` | `Audit:Enabled` |
| `AUDIT_FILE_PATH` | `Audit:FilePath` |
| `AUDIT_MAX_FILE_SIZE_MB` | `Audit:MaxFileSizeMB` |
| `AUDIT_MAX_BACKUP_FILES` | `Audit:MaxBackupFiles` |
| `HEALTH_READY_CACHE_SECONDS` | `Health:ReadyCacheSeconds` |
| `METRICS_ENABLED` | `Metrics:Enabled` |
| `METRICS_PATH` | `Metrics:Path` |
| `RATE_LIMIT_ENABLED` | `RateLimit:Enabled` |
| `RATE_LIMIT_PERMIT` | `RateLimit:PermitLimit` |
| `RATE_LIMIT_WINDOW_SECONDS` | `RateLimit:WindowSeconds` |
| `SHUTDOWN_TIMEOUT_SECONDS` | `Shutdown:ShutdownTimeoutSeconds` |
| `SHUTDOWN_ON_UNHANDLED_EXCEPTION` | `Shutdown:ShutdownOnUnhandledException` |
| `TAIGA_URL` | `Taiga:BaseUrl` |
| `TAIGA_USERNAME` | `Taiga:Username` |
| `TAIGA_PASSWORD` | `Taiga:Password` |
| `TAIGA_TOKEN` | `Taiga:Token` |
| `TAIGA_REFRESH_TOKEN` | `Taiga:RefreshToken` |
| `TAIGA_AUTH_METHOD` | `Taiga:AuthMethod` |
| `TAIGA_AUTH_SCHEME` | `Taiga:AuthScheme` |
| `TAIGA_COOKIE` | `Taiga:Cookie` |
| `TAIGA_CA_CERT_PATH` | `Taiga:CaCertificatePath` |
| `TAIGA_IGNORE_SSL_ERRORS` | `Taiga:IgnoreSslErrors` |
| `TAIGA_TIMEOUT_SECONDS` | `Taiga:TimeoutSeconds` |
| `TAIGA_MAX_RETRIES` | `Taiga:MaxRetries` |
| `TAIGA_RETRY_DELAY_MS` | `Taiga:RetryBaseDelayMs` |

### Example `appsettings.Local.json`

```json
{
  "Mcp": {
    "AuthToken": "<generate a random token>"
  },
  "Taiga": {
    "BaseUrl": "https://taiga.example.local/api/v1",
    "Username": "user",
    "Password": "password",
    "CaCertificatePath": "certs/taiga_leaf.pem",
    "IgnoreSslErrors": false
  }
}
```

## Taiga Authentication

`AuthMethod` determines how the token is obtained:

- `auto` (default) — first the configured `Token`, then `RefreshToken`, then username/password login;
- `password` — always log in with username/password;
- `token` — use only the configured `Token` (no API login; handy for SSO, where the token comes from an open Taiga session);
- `cookie` — send the `Cookie` header (SSO/proxy session); `Authorization` is not used.

The token is cached in a single place (a singleton) and reused across calls; on `401` a single automatic
refresh (token refresh or re-login) is performed.

## Corporate / Self-Signed Certificate

If the OS does not trust the Taiga server certificate, use one of the following:

1. **Certificate pinning** (preferred): provide the server certificate (PEM/DER/PFX) and set `CaCertificatePath`.
   Validation succeeds on an exact thumbprint match; if the file is a root CA, the chain is validated instead.
2. **Disable validation**: `IgnoreSslErrors=true`. Insecure (MITM risk), for trusted networks only.

A renewed certificate must be provided when it is rotated.

## Connecting an MCP Client

Example configuration (the exact schema depends on the client; the transport is Streamable HTTP):

```json
{
  "mcpServers": {
    "taiga": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:5000/",
      "headers": { "X-Api-Key": "<MCP_AUTH_TOKEN>" }
    }
  }
}
```

If `Mcp:AuthToken` is set, pass it in the `X-Api-Key` header (or `Authorization: Bearer <token>`).
Without the token, MCP requests receive `401`. The health endpoints are always open.

## Health Checks

| Endpoint | Purpose | Response |
| ---------- | --------- | ---------- |
| `/health` | General check (backwards compatible). | `ok` |
| `/health/live` | Liveness: the process is alive and responding. | `ok` |
| `/health/ready` | Readiness: configured and can reach the Taiga API. The result is cached (`Health:ReadyCacheSeconds`, 60 s by default), so the real `users/me` request runs at most once per interval. | `{"status":"Healthy"}` or `{"status":"Unhealthy","reason":"..."}` (HTTP 503). |

The health endpoints are not protected by the MCP token and are not part of MCP.

## Metrics and Rate Limiting

- `GET /metrics` serves Prometheus-format metrics (the `Metrics:Path` value). Like health, the endpoint is open and requires no token; restrict network access to it if needed.
- Requests to the MCP endpoint are limited by the `RateLimit` policy (see above): the key is the token (`X-Api-Key`/`Bearer`) or the client IP. When exceeded, `429` with a `Retry-After` header is returned.

## Shutdown

- On `SIGTERM`/`Ctrl+C` the server shuts down gracefully, waiting for in-flight requests no longer than `Shutdown:ShutdownTimeoutSeconds` (30 sec by default).
- On an unhandled exception (when `Shutdown:ShutdownOnUnhandledException=true`) a `Critical` entry is logged and a graceful shutdown is triggered.

## Docker

The image installs the app in `/opt/mcp` and runs it as the non-root `app` user (UID 1654). Startup parameters come from environment variables; configuration and certificates are mounted as files.

Build:

```bash
docker build -t taiga-mcp:latest .
```

Run — parameters via ENV/`--env-file`, config and certificate via mounts:

```bash
docker run -d --name taiga-mcp \
  -p 8080:8080 \
  --env-file .env \
  -v "$PWD/appsettings.Local.json:/opt/mcp/appsettings.Local.json:ro" \
  -v "$PWD/certs/taiga_leaf.pem:/opt/mcp/certs/taiga_leaf.pem:ro" \
  taiga-mcp:latest
```

Minimal `.env` (not committed):

```dotenv
MCP_AUTH_TOKEN=...
TAIGA_URL=https://taiga.example.local/api/v1
TAIGA_USERNAME=user
TAIGA_PASSWORD=...
TAIGA_CA_CERT_PATH=/opt/mcp/certs/taiga_leaf.pem
```

Notes:

- `MCP_HOST`/`MCP_PORT` default to `0.0.0.0`/`8080` inside the image; map the external port with `-p`.
- The audit log directory `/opt/mcp/logs` is owned by the `app` user; put it on a volume if needed: `-v taiga-logs:/opt/mcp/logs`.
- The certificate is not baked into the image — mount it at runtime. If no corporate certificate is needed, clear `TAIGA_CA_CERT_PATH`.
- No secrets are baked in: `.dockerignore` excludes `appsettings.Local.json`, `.env`, and `certs/`.

## Security

- Keep secrets in `appsettings.Local.json` (listed in `.gitignore`) or in environment variables / a secret manager. Never commit them.
- The server listens on `0.0.0.0` (all interfaces) by default. Always set `Mcp:AuthToken` when the server is reachable from the network; restrict the address to `127.0.0.1` for local use only.
- Prefer certificate pinning over `IgnoreSslErrors`.
- Destructive tools (`taiga_delete_*`, `taiga_delete_epic_related_userstory`) are written to the audit log (`Audit:FilePath`).
- Rate-limit the MCP endpoint (`RateLimit` section) to protect against overload and token guessing.
- The `/health*` and `/metrics` endpoints are open (no MCP token) — restrict them at the proxy/firewall level when exposing the server to a network.
- Be aware that tools can perform irreversible actions (`taiga_delete_*`, `taiga_register_user`,
  `taiga_request_password_recovery`); grant the Taiga service account the minimum required permissions.

## Limitations

- The SSO cookie auth mode does not add the `X-CSRFToken` header — some setups may require further work.
- `/health/ready` does not poll Taiga on every request: the result is cached (`Health:ReadyCacheSeconds`, 60 s by default), so frequent polling causes no extra load.
- Rate limiting is stored in process memory; with multiple instances the limit applies per instance.
- No CI (build pipelines) is included; unit tests are run manually.

## Testing

The `tests/TaigaMcp.Tests` project covers key paths with regression tests (xUnit, 75 tests):

- `ToolsTests` / `ToolsInvocationTests` — registration and invocation of all 72 tools;
- `TaigaClientTests` — authentication (including resilience to invalid JSON), retries, pagination;
- `TaigaHttpHandlerFactoryTests` — early validation and certificate handling (pinning);
- `TaigaAuthStoreTests` — thread safety of the token cache;
- `AuditLoggerTests` — audit log writing and rotation;
- `ReadinessProbeTests` — readiness caching (`ReadyCacheSeconds`);
- `RateLimitSetupTests`, `MetricsTests`, `GracefulShutdownTests`, `HealthChecksTests`, `OptionsDefaultsTests`.

```bash
dotnet test .\tests\TaigaMcp.Tests\TaigaMcp.Tests.csproj -c Release
```

Collect coverage (coverlet):

```bash
dotnet test .\tests\TaigaMcp.Tests\TaigaMcp.Tests.csproj -c Release \
  --collect:"XPlat Code Coverage" --results-directory .\TestResults
```

## Technologies

- [.NET 8.0](https://dotnet.microsoft.com/)
- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol)
- [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore)
- [prometheus-net.AspNetCore](https://www.nuget.org/packages/prometheus-net.AspNetCore)
- [Microsoft.Extensions.Logging](https://learn.microsoft.com/dotnet/core/extensions/logging)
- [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/)

## Links

- [Model Context Protocol](https://modelcontextprotocol.io/) — the official MCP site
- [Taiga REST API](https://docs.taiga.io/) — Taiga API documentation
- [.NET Documentation](https://learn.microsoft.com/dotnet/) — .NET documentation

## License

This project is distributed under the **MIT** license — see [LICENSE.txt](LICENSE.txt).

---

**Last updated:** 2026
