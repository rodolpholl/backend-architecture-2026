# Kong + Keycloak — JWT RS256 Integration

> **Objective:** Describe how Kong authenticates requests using JWT RS256 issued by Keycloak.
> **Status:** Automatic configuration via `kong-init`

---

## Overview

Kong validates JWT tokens using the **Keycloak RS256 public key**, registered as a credential in Kong's native `jwt` plugin. No OIDC plugin is necessary.

Additionally, Kong injects the `X-Subscription-Key` header automatically via `request-transformer`, which is then validated by `SubscriptionKeyMiddleware` inside each .NET API.

```
Client
    |
    | GET /Consolidations/saldo  +  Authorization: Bearer <JWT RS256>
    v
Kong Gateway (:8000)
    |-- 1. jwt plugin:               validates RS256 signature with Keycloak public key
    |-- 2. request-transformer:      injects X-Subscription-Key in upstream header
    |-- 3. rate-limiting:            300 req/min (Entries) | 55 req/s (Consolidation)
    |-- 4. proxy-cache:              cache GET /Consolidations for 30s
    |
    v (if JWT valid)
FinControl.Entries.API (:5083)  or  FinControl.Consolidation.API (:5260)
    |
    v
SubscriptionKeyMiddleware (second internal validation layer)
```

---

## Configured Kong Plugins

| Plugin | Scope | Configuration |
|--------|--------|--------------|
| `jwt` | Global | RS256; `key_claim_name = iss`; Keycloak public key registered per consumer |
| `request-transformer` | Per service | Injects `X-Subscription-Key` upstream |
| `rate-limiting` | Per route | 300 req/min (Entries) · 55 req/s (Consolidation) |
| `proxy-cache` | Consolidation | Cache GET for 30s; `cache-control` strategy |

---

## Services and Routes

| Service | Internal URL (Docker) | Route path |
|---------|----------------------|-----------|
| `fincontrol-Entries` | `http://host.docker.internal:5083` | `/Entries` |
| `fincontrol-Consolidations` | `http://host.docker.internal:5260` | `/Consolidations` |

---

## How the JWT Plugin Works with Keycloak

Kong's `jwt` plugin validates the token without calling Keycloak at runtime — it uses the pre-registered public key:

```
Keycloak issues JWT (RS256)
    └─ Header: { "alg": "RS256", "kid": "..." }
    └─ Claims: { "iss": "http://localhost:8081/realms/fincontrol", "sub": "...", ... }

Kong receives the token:
    1. Reads the claim configured in key_claim_name (default: iss)
    2. Finds consumer with JWT credential corresponding to the issuer
    3. Validates signature with registered public key
    4. If valid → forwards to upstream
    5. If invalid → HTTP 401
```

Keycloak's public key is obtained via:
```bash
curl -s http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs | jq '.keys[0]'
```

---

## Quick Start

### 1. Start infrastructure

```bash
docker-compose up -d
```

Wait for all init containers to exit with `Exited (0)`.

### 2. Verify Kong is configured

```bash
# List services
curl -s http://localhost:8001/services | jq '.data[].name'

# List active plugins
curl -s http://localhost:8001/plugins | jq '.data[] | {name, enabled}'
```

Expected result:
```json
{"name": "jwt",                "enabled": true}
{"name": "request-transformer","enabled": true}
{"name": "rate-limiting",      "enabled": true}
{"name": "proxy-cache",        "enabled": true}
```

### 3. Get JWT token from Keycloak

```bash
TOKEN=$(curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend" \
  -d "client_secret=fincontrol-backend-secret-12345" \
  -d "grant_type=password" \
  -d "username=admin.fincontrol" \
  -d "password=Admin@123456" \
  | jq -r '.access_token')
```

### 4. Make authenticated request

```bash
# Entries
curl -s -w "\nHTTP %{http_code}\n" \
  http://localhost:8000/Entries/health \
  -H "Authorization: Bearer $TOKEN"

# Consolidation
curl -s -w "\nHTTP %{http_code}\n" \
  "http://localhost:8000/Consolidations/saldo?data-lancamento=2026-05-23" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Manual Configuration (if kong-init fails)

### Create Service — Entries

```bash
curl -X POST http://localhost:8001/services \
  -d name=fincontrol-Entries \
  -d url=http://host.docker.internal:5083
```

### Create Service — Consolidation

```bash
curl -X POST http://localhost:8001/services \
  -d name=fincontrol-Consolidations \
  -d url=http://host.docker.internal:5260
```

### Create Routes

```bash
curl -X POST http://localhost:8001/services/fincontrol-Entries/routes \
  -d name=Entries-route \
  -d "paths[]=/Entries"

curl -X POST http://localhost:8001/services/fincontrol-Consolidations/routes \
  -d name=Consolidations-route \
  -d "paths[]=/Consolidations"
```

### Enable JWT Plugin

```bash
curl -X POST http://localhost:8001/plugins \
  -d name=jwt \
  -d "config.claims_to_verify[]=exp" \
  -d "config.key_claim_name=iss"
```

### Create Consumer with JWT credential (Keycloak public key)

```bash
# Create consumer
curl -X POST http://localhost:8001/consumers \
  -d username=fincontrol-keycloak-consumer

# Register RS256 public key
# (replace RSA_PUBLIC_KEY with public key obtained from Keycloak JWKS)
curl -X POST http://localhost:8001/consumers/fincontrol-keycloak-consumer/jwt \
  -d algorithm=RS256 \
  -d "key=http://localhost:8081/realms/fincontrol" \
  -d "rsa_public_key=<RSA_PUBLIC_KEY>"
```

### Enable Rate Limiting

```bash
# Entries — 300 req/min
curl -X POST http://localhost:8001/services/fincontrol-Entries/plugins \
  -d name=rate-limiting \
  -d "config.minute=300" \
  -d "config.policy=local"

# Consolidation — 55 req/s (3300 req/min)
curl -X POST http://localhost:8001/services/fincontrol-Consolidations/plugins \
  -d name=rate-limiting \
  -d "config.second=55" \
  -d "config.policy=local"
```

### Enable Proxy Cache (Consolidation)

```bash
curl -X POST http://localhost:8001/services/fincontrol-Consolidations/plugins \
  -d name=proxy-cache \
  -d "config.response_code[]=200" \
  -d "config.request_method[]=GET" \
  -d "config.cache_ttl=30"
```

---

## Troubleshooting

| Issue | Solution |
|----------|---------|
| `No API key found in request` | JWT is missing or malformed in `Authorization` header |
| `Invalid signature` | Public key registered in Kong does not match token issuer |
| `Unauthorized` (401) | Token expired (TTL: 300s) — use `refresh_token` to renew (see [REFRESH_TOKEN_FLOW.md](REFRESH_TOKEN_FLOW.md)) |
| `403 Rate limit exceeded` | Wait or adjust `config.minute`/`config.second` in plugin |
| kong-init failed | `docker-compose logs kong-init` for diagnostics |
| JWKS not available | Verify Keycloak is healthy: `http://localhost:8081/health/ready` |

---

## References

- [Kong JWT Plugin](https://docs.konghq.com/hub/kong-inc/jwt/)
- [Kong Rate Limiting Plugin](https://docs.konghq.com/hub/kong-inc/rate-limiting/)
- [Kong Proxy Cache Plugin](https://docs.konghq.com/hub/kong-inc/proxy-cache/)
- [Keycloak JWKS](https://www.keycloak.org/docs/latest/securing_apps/#_certificate_endpoint)
- [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md)
- [KONG_KEYCLOAK_TESTS.md](KONG_KEYCLOAK_TESTS.md)
- [REFRESH_TOKEN_FLOW.md](REFRESH_TOKEN_FLOW.md) — token lifecycle and refresh flow in client

---

**Version:** 3.0
**Last updated:** May 2026
**Status:** Active — automated configuration via kong-init

