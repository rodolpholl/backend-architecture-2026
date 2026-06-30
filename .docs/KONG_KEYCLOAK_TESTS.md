# Kong + Keycloak — Integration Tests

> **Objective:** Verify that Kong is properly protecting APIs via JWT RS256 + rate-limiting.
> **Audience:** Developers and SREs

## Prerequisites

- `docker-compose up -d` executed and all inits with `Exited (0)`
- Kong responding: `http://localhost:8001/status` (HTTP 200)
- Keycloak responding: `http://localhost:8081/health/ready` (HTTP 200)
- `fincontrol` realm and clients configured (see [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md))

---

## Test 1: Verify Services and Routes in Kong

**Objective:** Confirm that kong-init created services and routes with correct URLs.

**PowerShell:**
```powershell
(Invoke-WebRequest -Uri "http://localhost:8001/services" -Method GET).Content |
  ConvertFrom-Json | Select-Object -ExpandProperty data |
  Format-Table name, url
```

**Bash:**
```bash
curl -s http://localhost:8001/services | jq '.data[] | {name, url}'
```

**Expected result:**
```
fincontrol-Entries  →  http://host.docker.internal:5083
fincontrol-Consolidations →  http://host.docker.internal:5260
```

PASS → Services with correct URLs (`5083` / `5260`)
FAIL → See `docker-compose logs kong-init`

---

## Test 2: Verify Active Plugins

**Objective:** Confirm that `jwt`, `request-transformer`, `rate-limiting` and `proxy-cache` are enabled.

**Bash:**
```bash
curl -s http://localhost:8001/plugins | jq '.data[] | {name, enabled}'
```

**Expected result:**
```json
{"name": "jwt",                 "enabled": true}
{"name": "request-transformer", "enabled": true}
{"name": "rate-limiting",       "enabled": true}
{"name": "proxy-cache",         "enabled": true}
```

---

## Test 3: Get JWT Token from Keycloak

**Objective:** Authenticate with Keycloak and obtain RS256 JWT.

**PowerShell:**
```powershell
$params = @{
    Uri     = "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token"
    Method  = "POST"
    Headers = @{"Content-Type" = "application/x-www-form-urlencoded"}
    Body    = "client_id=fincontrol-backend&client_secret=fincontrol-backend-secret-12345&grant_type=password&username=admin.fincontrol&password=Admin@123456"
}
$TOKEN = (Invoke-WebRequest @params).Content | ConvertFrom-Json | Select-Object -ExpandProperty access_token
Write-Host "Token: $($TOKEN.Substring(0,50))..."
```

**Bash:**
```bash
TOKEN=$(curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend&client_secret=fincontrol-backend-secret-12345&grant_type=password&username=admin.fincontrol&password=Admin@123456" \
  | jq -r '.access_token')
echo "Token: ${TOKEN:0:50}..."
```

PASS → JWT token returned (long string starting with `eyJ...`)
FAIL → Check credentials in [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md)

---

## Test 4: Access Without JWT Token (should be blocked)

**Objective:** Confirm that Kong rejects requests without `Authorization: Bearer`.

**Bash:**
```bash
curl -s -o /dev/null -w "%{http_code}" \
  http://localhost:8000/Entries/health
# Expected: 401
```

PASS → HTTP 401 with `{"message":"Unauthorized"}`
FAIL → `jwt` plugin not active — see Test 2

---

## Test 5: Access With Valid JWT Token (should pass)

**Objective:** Confirm that Kong forwards requests with valid JWT.

**PowerShell:**
```powershell
$response = Invoke-WebRequest -Uri "http://localhost:8000/Entries/health" `
    -Headers @{"Authorization" = "Bearer $TOKEN"}
Write-Host "Status: $($response.StatusCode)"
```

**Bash:**
```bash
curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/Entries/health
# Expected: 200
```

PASS → HTTP 200
FAIL → Check token and `jwt` plugin configuration in Kong

---

## Test 6: Invalid JWT Token (should be rejected)

**Bash:**
```bash
curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.invalid.invalid" \
  http://localhost:8000/Entries/health
# Expected: 401
```

PASS → HTTP 401
FAIL → `jwt` plugin not validating signature

---

## Test 7: Keycloak JWKS (RS256 public key)

**Objective:** Confirm that the public key used to validate tokens is available.

**Bash:**
```bash
curl -s http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs | \
  jq '.keys[] | {kty, alg, use, kid}'
```

**Expected result:**
```json
{
  "kty": "RSA",
  "alg": "RS256",
  "use": "sig",
  "kid": "..."
}
```

---

## Test 8: Keycloak OIDC Discovery

**Objective:** Confirm Keycloak endpoints.

**Bash:**
```bash
curl -s http://localhost:8081/realms/fincontrol/.well-known/openid-configuration | \
  jq '{issuer, token_endpoint, jwks_uri}'
```

**Expected result:**
```json
{
  "issuer": "http://localhost:8081/realms/fincontrol",
  "token_endpoint": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token",
  "jwks_uri": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs"
}
```

---

## Test 9: Rate Limiting (Consolidation — 55 req/s)

**Objective:** Confirm that rate limiting is active in response headers.

**Bash:**
```bash
curl -s -I \
  -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8000/Consolidations/saldo?data-lancamento=2026-05-23" \
  | grep -i "ratelimit"
# Expected: X-RateLimit-Limit-Second: 55
```

---

## Report Template

```
# Kong + Keycloak Integration Test Report
Date: [DATE]

| Test | Status | Details |
|-------|--------|----------|
| 1. Services/Routes (ports 5083/5260)  | PASS/FAIL | |
| 2. Active plugins (jwt + rate-limiting)| PASS/FAIL | |
| 3. Keycloak RS256 token                | PASS/FAIL | |
| 4. Without JWT → blocked               | PASS/FAIL | HTTP 401 expected |
| 5. With valid JWT → passes             | PASS/FAIL | HTTP 200 expected |
| 6. Invalid JWT → blocked               | PASS/FAIL | HTTP 401 expected |
| 7. JWKS available                      | PASS/FAIL | |
| 8. OIDC Discovery                      | PASS/FAIL | |
| 9. Rate limiting in headers            | PASS/FAIL | |
```

---

## Troubleshooting

| Issue | Solution |
|----------|---------|
| `Connection refused` on :8000 | Kong not running — `docker-compose ps kong` |
| `Unauthorized` with valid token | Check issuer — must be `http://localhost:8081/realms/fincontrol` |
| Token expired | Renew token (default TTL: 300s) |
| Services with wrong ports | `docker-compose logs kong-init` — verify if using `5083`/`5260` |
| `502 Bad Gateway` | .NET API not running — start APIs (see [../README.md](../README.md)) |
| kong-init did not complete | `docker-compose logs kong-init` |

---

**Version:** 3.0
**Last updated:** May 2026
**Status:** Active

