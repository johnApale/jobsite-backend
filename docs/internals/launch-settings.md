# Launch Settings

**File:** `Properties/launchSettings.json`

Defines how `dotnet run` starts the application in development.

## Profiles

### `http`

```json
{
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "http://localhost:5166",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

- HTTP only, port **5166**.
- No browser launch (headless / API-first development).
- To use this profile: `dotnet run` or `dotnet run --launch-profile http`.

### `https`

```json
{
  "commandName": "Project",
  "dotnetRunMessages": true,
  "launchBrowser": false,
  "applicationUrl": "https://localhost:7118;http://localhost:5166",
  "environmentVariables": {
    "ASPNETCORE_ENVIRONMENT": "Development"
  }
}
```

- HTTPS on port **7118**, HTTP on port **5166**.
- Requires a dev certificate: `dotnet dev-certs https --trust`.
- To use this profile: `dotnet run --launch-profile https`.

## Key URLs in development

| URL                                     | Purpose                     |
| --------------------------------------- | --------------------------- |
| `http://localhost:5166/health`          | Liveness probe              |
| `http://localhost:5166/ready`           | Readiness probe             |
| `http://localhost:5166/scalar/v1`       | Scalar API documentation UI |
| `http://localhost:5166/openapi/v1.json` | Raw OpenAPI spec            |

## Notes

- Both profiles set `ASPNETCORE_ENVIRONMENT=Development`, which enables the Scalar UI and OpenAPI endpoint (gated by `app.Environment.IsDevelopment()` in `Program.cs`).
- `launchBrowser` is `false` in both profiles — open the Scalar UI manually at `/scalar/v1`.
- Tenant resolution requires a subdomain in the `Host` header. When calling tenant-scoped endpoints from `localhost`, either:
  - Add a hosts file entry (e.g. `127.0.0.1 acme.localhost`) and use `http://acme.localhost:5166`.
  - Pass a custom `Host` header: `curl -H "Host: acme.djobsite.com" http://localhost:5166/api/v1/...`.
