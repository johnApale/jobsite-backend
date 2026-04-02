# Health Endpoints

Infrastructure probes for load balancers, orchestrators, and monitoring systems. These endpoints are **excluded from the OpenAPI specification** and do not appear in Scalar.

## `GET /health`

Liveness probe. Returns 200 if the application process is running.

**Authentication:** None
**Tenant resolution:** Bypassed

### Response

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "status": "healthy"
}
```

## `GET /ready`

Readiness probe. Returns 200 if the application is ready to accept traffic.

**Authentication:** None
**Tenant resolution:** Bypassed

### Response

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "status": "ready"
}
```

## Usage

### Kubernetes

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 5166
  initialDelaySeconds: 5
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /ready
    port: 5166
  initialDelaySeconds: 10
  periodSeconds: 5
```

### Docker Compose

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5166/health"]
  interval: 10s
  timeout: 3s
  retries: 3
```
