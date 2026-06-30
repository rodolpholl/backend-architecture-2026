# Refresh Token — Flow and Responsibilities

> **Objective:** Document the JWT token lifecycle in FinControl, where refresh_token should be handled and how to implement the correct flow in clients.
> **Audience:** Frontend, mobile and API integration developers

---

## Architectural Decision

Kong uses the **`jwt` (built-in)** plugin, which is **stateless**: it validates RS256 signature, issuer and lifetime of the `access_token`, but **does not perform automatic refresh**. This is intentional and aligned with OAuth 2.0/OIDC standard:

| Layer | Responsibility |
|--------|-----------------|
| **Keycloak** | Issues `access_token` + `refresh_token`; renews tokens via `/token` |
| **Kong** | Validates `access_token`; returns `401` if expired |
| **.NET APIs** | Revalidate JWT (defense-in-depth); never see `refresh_token` |
| **Client** | Detects `401`, uses `refresh_token` to obtain new `access_token` |

The `refresh_token` **never transits through Kong or APIs** — it is an exclusive secret between the client and Keycloak.

---

## Configured Lifetimes

Configured in `fincontrol` realm via [keycloak-init.sh](../docker-init/keycloak/keycloak-init.sh):

| Parameter | Value | Meaning |
|-----------|-------|-------------|
| `accessTokenLifespan` | **300s (5 min)** | Validity of `access_token` sent to Kong |
| `ssoSessionIdleTimeout` | **1800s (30 min)** | `refresh_token` expires if inactive for 30 min |
| `ssoSessionMaxLifespan` | **36000s (10 h)** | Maximum total session; after this, new login required |
| `refreshTokenMaxReuse` | **0** | Each `refresh_token` is valid for **a single use** (automatic rotation) |

> **Warning:** `refreshTokenMaxReuse: 0` means that when using the `refresh_token`, Keycloak invalidates it and returns a **new** `refresh_token`. The client must always store the most recent token.

---

## Complete Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. INITIAL LOGIN                                                    │
│                                                                     │
│  Client ──POST /token──► Keycloak                                  │
│          grant_type=password (or authorization_code + PKCE)        │
│                                                                     │
│  Keycloak ◄── access_token  (valid 5 min)                          │
│           ◄── refresh_token (valid 30 min idle / 10h max)          │
│           ◄── expires_in: 300                                       │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          │ store both tokens
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  2. NORMAL REQUEST (valid token)                                    │
│                                                                     │
│  Client ──GET /Consolidations──► Kong                                │
│          Authorization: Bearer <access_token>                       │
│                                                                     │
│  Kong validates JWT ──► forwards upstream                          │
│  API returns ◄── 200 OK                                             │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          │ after ~5 min
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  3. TOKEN EXPIRED — REFRESH                                         │
│                                                                     │
│  Client ──GET /Consolidations──► Kong                                │
│          Authorization: Bearer <expired access_token>              │
│                                                                     │
│  Kong ◄── 401 Unauthorized                                          │
│          {"message":"Unauthorized"}                                │
│                                                                     │
│  Client detects 401 ──POST /token──► Keycloak                      │
│          grant_type=refresh_token                                  │
│          refresh_token=<current refresh_token>                      │
│                                                                     │
│  Keycloak ◄── new access_token  (valid for another 5 min)          │
│           ◄── new refresh_token  (previous one was invalidated)     │
│                                                                     │
│  Client stores new tokens                                          │
│  Client ──GET /Consolidations──► Kong  (retry with new access_token) │
│  API returns ◄── 200 OK                                             │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          │ after 30 min idle or 10h
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  4. REFRESH TOKEN EXPIRED — NEW LOGIN                               │
│                                                                     │
│  POST /token with expired refresh_token                            │
│  Keycloak ◄── 400 Bad Request                                       │
│           {"error":"invalid_grant","error_description":"..."}       │
│                                                                     │
│  Client redirects user to new login                                │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Get Tokens (Initial Login)

### Frontend Client — `fincontrol-frontend` (public, PKCE)

**Bash:**
```bash
RESPONSE=$(curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-frontend" \
  -d "grant_type=password" \
  -d "username=user.fincontrol" \
  -d "password=User@123456" \
  -d "scope=openid profile email")

ACCESS_TOKEN=$(echo "$RESPONSE"  | jq -r '.access_token')
REFRESH_TOKEN=$(echo "$RESPONSE" | jq -r '.refresh_token')
EXPIRES_IN=$(echo "$RESPONSE"    | jq -r '.expires_in')

echo "access_token expires in: ${EXPIRES_IN}s"
```

**PowerShell:**
```powershell
$body = "client_id=fincontrol-frontend&grant_type=password" +
        "&username=user.fincontrol&password=User@123456&scope=openid profile email"

$response = Invoke-WebRequest `
  -Uri    "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token" `
  -Method POST `
  -Headers @{"Content-Type" = "application/x-www-form-urlencoded"} `
  -Body   $body |
  ConvertFrom-Json

$ACCESS_TOKEN  = $response.access_token
$REFRESH_TOKEN = $response.refresh_token
Write-Host "Expires in: $($response.expires_in)s"
```

### Backend / Service Client — `fincontrol-backend` (confidential)

**Bash:**
```bash
RESPONSE=$(curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend" \
  -d "client_secret=fincontrol-backend-secret-12345" \
  -d "grant_type=password" \
  -d "username=admin.fincontrol" \
  -d "password=Admin@123456" \
  -d "scope=openid profile email")

ACCESS_TOKEN=$(echo "$RESPONSE"  | jq -r '.access_token')
REFRESH_TOKEN=$(echo "$RESPONSE" | jq -r '.refresh_token')
```

---

## Renew Token (Refresh)

### Frontend — `fincontrol-frontend` (no client_secret)

**Bash:**
```bash
RESPONSE=$(curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-frontend" \
  -d "grant_type=refresh_token" \
  -d "refresh_token=${REFRESH_TOKEN}")

NEW_ACCESS_TOKEN=$(echo "$RESPONSE"  | jq -r '.access_token')
NEW_REFRESH_TOKEN=$(echo "$RESPONSE" | jq -r '.refresh_token')

# Always replace — the previous refresh_token was invalidated
ACCESS_TOKEN=$NEW_ACCESS_TOKEN
REFRESH_TOKEN=$NEW_REFRESH_TOKEN
```

**PowerShell:**
```powershell
$body = "client_id=fincontrol-frontend&grant_type=refresh_token&refresh_token=$REFRESH_TOKEN"

$response = Invoke-WebRequest `
  -Uri    "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token" `
  -Method POST `
  -Headers @{"Content-Type" = "application/x-www-form-urlencoded"} `
  -Body   $body |
  ConvertFrom-Json

$ACCESS_TOKEN  = $response.access_token
$REFRESH_TOKEN = $response.refresh_token   # Always save the new one
```

### Backend — `fincontrol-backend` (with client_secret)

**Bash:**
```bash
RESPONSE=$(curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend" \
  -d "client_secret=fincontrol-backend-secret-12345" \
  -d "grant_type=refresh_token" \
  -d "refresh_token=${REFRESH_TOKEN}")

NEW_ACCESS_TOKEN=$(echo "$RESPONSE"  | jq -r '.access_token')
NEW_REFRESH_TOKEN=$(echo "$RESPONSE" | jq -r '.refresh_token')
```

---

## Detect When to Refresh

There are two strategies, which can be combined:

### Strategy 1 — Proactive (recommended)

Calculate the expiration moment when receiving the token and renew before expiring:

```bash
# Store the expiration instant (now + expires_in - 30s margin)
EXPIRES_AT=$(( $(date +%s) + EXPIRES_IN - 30 ))

# Before each request:
if [ "$(date +%s)" -ge "$EXPIRES_AT" ]; then
  # Renew token before sending the request
  refresh_tokens
fi
```

```csharp
// Example C# — check before request
if (DateTimeOffset.UtcNow >= _tokenExpiresAt.AddSeconds(-30))
    await RefreshTokenAsync(cancellationToken);
```

### Strategy 2 — Reactive (fallback)

Intercept Kong's `401` and try refresh once:

```bash
HTTP_STATUS=$(curl -s -o response.json -w "%{http_code}" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  "http://localhost:8000/Consolidations/saldo?data-lancamento=2026-05-22")

if [ "$HTTP_STATUS" -eq "401" ]; then
  echo "Token expired — refreshing..."
  refresh_tokens
  # Retry with new token
  curl -s -H "Authorization: Bearer $ACCESS_TOKEN" \
    "http://localhost:8000/Consolidations/saldo?data-lancamento=2026-05-22"
fi
```

> **Warning:** The reactive strategy alone can cause failure on the first request after expiration. Use proactive as primary and reactive as safety fallback.

---

## Check if Refresh Token is Valid

To check if the `refresh_token` is still valid before using it (session verification):

**Bash:**
```bash
curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/token/introspect \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend" \
  -d "client_secret=fincontrol-backend-secret-12345" \
  -d "token=${REFRESH_TOKEN}" \
  | jq '{active, exp}'
```

Expected result when valid:
```json
{
  "active": true,
  "exp": 1748300000
}
```

When invalid or expired:
```json
{
  "active": false
}
```

---

## Logout (Invalidate Refresh Token)

To invalidate the session and active `refresh_token`:

**Bash:**
```bash
curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/logout \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-frontend" \
  -d "refresh_token=${REFRESH_TOKEN}"
# Expected: HTTP 204 No Content
```

---

## Refresh Token Rotation

With `refreshTokenMaxReuse: 0` configured in the realm, Keycloak applies **automatic refresh token rotation**:

```
Login
  └─► refresh_token_A  (valid)

First refresh with refresh_token_A
  └─► refresh_token_A  (INVALIDATED)
  └─► refresh_token_B  (new, valid)

Second refresh with refresh_token_B
  └─► refresh_token_B  (INVALIDATED)
  └─► refresh_token_C  (new, valid)

Attempt to reuse refresh_token_A (already invalidated)
  └─► 400 Bad Request {"error":"invalid_grant"}
      → Keycloak invalidates ENTIRE session (protection against token theft)
```

> **Important consequence:** If a `refresh_token` is used twice (e.g., race condition in multi-tab client), Keycloak invalidates the entire session. The client must serialize refresh calls.

---

## Pseudocode — Client Implementation

```
TokenManager:
  accessToken: string
  refreshToken: string
  expiresAt: DateTime

  initialize(username, password):
    response = POST /token (grant_type=password)
    store(response)

  store(response):
    accessToken  = response.access_token
    refreshToken = response.refresh_token
    expiresAt    = now() + response.expires_in - 30s  // 30s margin

  isExpired():
    return now() >= expiresAt

  refresh():
    response = POST /token (grant_type=refresh_token, refresh_token=refreshToken)
    if response.error == "invalid_grant":
      triggerReLogin()   // session expired — new login required
    else:
      store(response)

  getAccessToken():
    if isExpired():
      refresh()          // proactive
    return accessToken

  request(url, method):
    token = getAccessToken()
    response = HTTP(url, Authorization: Bearer token)
    if response.status == 401:
      refresh()          // reactive (fallback)
      token = getAccessToken()
      response = HTTP(url, Authorization: Bearer token)
    return response
```

---

## Troubleshooting

| Issue | Likely cause | Solution |
|----------|---------------|---------|
| `{"error":"invalid_grant"}` on refresh | `refresh_token` expired or already used | Redirect to new login |
| `{"error":"invalid_grant"}` unexpected | Race condition — token used twice | Serialize refresh calls; store most recent |
| Kong returns `401` with freshly renewed token | `ClockSkew` — clock mismatch between servers | APIs tolerate 30s skew (`ClockSkew = TimeSpan.FromSeconds(30)`) |
| `{"message":"Unauthorized"}` from Kong | `access_token` expired or malformed | Check token `exp` at [jwt.io](https://jwt.io); refresh |
| Refresh returns `400` in confidential client | `client_secret` missing or incorrect | Include `client_secret` in refresh request body |
| `{"error":"unauthorized_client"}` | `grant_type=refresh_token` not allowed for client | Check `standardFlowEnabled: true` in Keycloak client |

---

## Summary of Keycloak Endpoints

All token endpoints are available via OIDC Discovery:

```bash
curl -s http://localhost:8081/realms/fincontrol/.well-known/openid-configuration \
  | jq '{token_endpoint, end_session_endpoint, introspection_endpoint}'
```

| Operation | Method | Endpoint |
|----------|--------|----------|
| Login / Get token | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/token` |
| Refresh token | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/token` |
| Logout | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/logout` |
| Introspection | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/token/introspect` |
| JWKS (public key) | GET | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs` |

---

## References

- [KONG_KEYCLOAK_OIDC.md](KONG_KEYCLOAK_OIDC.md) — JWT configuration in Kong
- [KONG_KEYCLOAK_TESTS.md](KONG_KEYCLOAK_TESTS.md) — integration tests
- [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md) — realm configuration
- [RFC 6749 — OAuth 2.0](https://datatracker.ietf.org/doc/html/rfc6749#section-6) — Refresh Token Grant
- [Keycloak — Session and Token Timeouts](https://www.keycloak.org/docs/latest/server_admin/#_timeouts)

---

**Version:** 1.0
**Last updated:** May 2026
**Status:** Active

