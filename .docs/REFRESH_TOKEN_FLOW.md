# Refresh Token — Fluxo e Responsabilidades

> **Objetivo:** Documentar o ciclo de vida dos tokens JWT no FinControl, onde o refresh_token deve ser tratado e como implementar o fluxo correto nos clientes.
> **Audiência:** Desenvolvedores de frontend, mobile e integradores de API

---

## Decisão Arquitetural

O Kong usa o plugin **`jwt` (built-in)**, que é **stateless**: valida assinatura RS256, issuer e lifetime do `access_token`, mas **não realiza refresh automático**. Isso é intencional e alinhado com o padrão OAuth 2.0/OIDC:

| Camada | Responsabilidade |
|--------|-----------------|
| **Keycloak** | Emite `access_token` + `refresh_token`; renova tokens via `/token` |
| **Kong** | Valida `access_token`; retorna `401` se expirado |
| **APIs .NET** | Revalidam JWT (defense-in-depth); nunca veem o `refresh_token` |
| **Cliente** | Detecta `401`, usa `refresh_token` para obter novo `access_token` |

O `refresh_token` **nunca transita pelo Kong nem pelas APIs** — é um segredo exclusivo entre o cliente e o Keycloak.

---

## Tempos de Vida Configurados

Configurados no realm `fincontrol` via [keycloak-init.sh](../docker-init/keycloak/keycloak-init.sh):

| Parâmetro | Valor | Significado |
|-----------|-------|-------------|
| `accessTokenLifespan` | **300s (5 min)** | Validade do `access_token` enviado ao Kong |
| `ssoSessionIdleTimeout` | **1800s (30 min)** | `refresh_token` expira se inativo por 30 min |
| `ssoSessionMaxLifespan` | **36000s (10 h)** | Sessão total máxima; após isso, novo login obrigatório |
| `refreshTokenMaxReuse` | **0** | Cada `refresh_token` é válido para **um único uso** (rotação automática) |

> **Atenção:** `refreshTokenMaxReuse: 0` significa que ao usar o `refresh_token`, o Keycloak o invalida e retorna um **novo** `refresh_token`. O cliente deve sempre armazenar o token mais recente.

---

## Fluxo Completo

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. LOGIN INICIAL                                                    │
│                                                                     │
│  Cliente ──POST /token──► Keycloak                                  │
│           grant_type=password (ou authorization_code + PKCE)        │
│                                                                     │
│  Keycloak ◄── access_token  (válido 5 min)                          │
│           ◄── refresh_token (válido 30 min idle / 10h máx)          │
│           ◄── expires_in: 300                                       │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          │ armazena ambos os tokens
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  2. REQUISIÇÃO NORMAL (token válido)                                 │
│                                                                     │
│  Cliente ──GET /consolidados──► Kong                                │
│           Authorization: Bearer <access_token>                       │
│                                                                     │
│  Kong valida JWT ──► encaminha ao upstream                          │
│  API retorna ◄── 200 OK                                             │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          │ após ~5 min
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  3. TOKEN EXPIRADO — REFRESH                                         │
│                                                                     │
│  Cliente ──GET /consolidados──► Kong                                │
│           Authorization: Bearer <access_token expirado>             │
│                                                                     │
│  Kong ◄── 401 Unauthorized                                          │
│           {"message":"Unauthorized"}                                │
│                                                                     │
│  Cliente detecta 401 ──POST /token──► Keycloak                      │
│           grant_type=refresh_token                                  │
│           refresh_token=<refresh_token atual>                        │
│                                                                     │
│  Keycloak ◄── novo access_token  (válido por mais 5 min)            │
│           ◄── novo refresh_token  (o anterior foi invalidado)        │
│                                                                     │
│  Cliente armazena os novos tokens                                   │
│  Cliente ──GET /consolidados──► Kong  (retry com novo access_token) │
│  API retorna ◄── 200 OK                                             │
└─────────────────────────────────────────────────────────────────────┘
                          │
                          │ após 30 min idle ou 10h
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│  4. REFRESH TOKEN EXPIRADO — NOVO LOGIN                              │
│                                                                     │
│  POST /token com refresh_token expirado                             │
│  Keycloak ◄── 400 Bad Request                                       │
│           {"error":"invalid_grant","error_description":"..."}       │
│                                                                     │
│  Cliente redireciona usuário para novo login                        │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Obter Tokens (Login Inicial)

### Cliente Frontend — `fincontrol-frontend` (público, PKCE)

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

echo "access_token expira em: ${EXPIRES_IN}s"
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
Write-Host "Expira em: $($response.expires_in)s"
```

### Cliente Backend / Serviço — `fincontrol-backend` (confidencial)

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

## Renovar o Token (Refresh)

### Frontend — `fincontrol-frontend` (sem client_secret)

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

# Sempre substituir — o refresh_token anterior foi invalidado
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
$REFRESH_TOKEN = $response.refresh_token   # Sempre salvar o novo
```

### Backend — `fincontrol-backend` (com client_secret)

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

## Detectar Quando Fazer Refresh

Há duas estratégias, podendo ser combinadas:

### Estratégia 1 — Proativa (recomendada)

Calcular o momento de expiração ao receber o token e renovar antes de expirar:

```bash
# Guarda o instante de expiração (agora + expires_in - margem de 30s)
EXPIRES_AT=$(( $(date +%s) + EXPIRES_IN - 30 ))

# Antes de cada requisição:
if [ "$(date +%s)" -ge "$EXPIRES_AT" ]; then
  # Renovar token antes de enviar a requisição
  refresh_tokens
fi
```

```csharp
// Exemplo C# — verificar antes da requisição
if (DateTimeOffset.UtcNow >= _tokenExpiresAt.AddSeconds(-30))
    await RefreshTokenAsync(cancellationToken);
```

### Estratégia 2 — Reativa (fallback)

Interceptar o `401` do Kong e tentar refresh uma vez:

```bash
HTTP_STATUS=$(curl -s -o response.json -w "%{http_code}" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  "http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-22")

if [ "$HTTP_STATUS" -eq "401" ]; then
  echo "Token expirado — renovando..."
  refresh_tokens
  # Retry com novo token
  curl -s -H "Authorization: Bearer $ACCESS_TOKEN" \
    "http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-22"
fi
```

> **Atenção:** A estratégia reativa isolada pode causar falha na primeira requisição após a expiração. Use a proativa como principal e a reativa como fallback de segurança.

---

## Verificar se o Refresh Token Está Válido

Para checar se o `refresh_token` ainda é válido antes de usá-lo (verificação de sessão):

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

Resultado esperado quando válido:
```json
{
  "active": true,
  "exp": 1748300000
}
```

Quando inválido ou expirado:
```json
{
  "active": false
}
```

---

## Logout (Invalidar Refresh Token)

Para invalidar a sessão e o `refresh_token` ativo:

**Bash:**
```bash
curl -s -X POST \
  http://localhost:8081/realms/fincontrol/protocol/openid-connect/logout \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-frontend" \
  -d "refresh_token=${REFRESH_TOKEN}"
# Esperado: HTTP 204 No Content
```

---

## Rotação de Refresh Tokens

Com `refreshTokenMaxReuse: 0` configurado no realm, o Keycloak aplica **rotação automática de refresh tokens**:

```
Login
  └─► refresh_token_A  (válido)

Primeiro refresh com refresh_token_A
  └─► refresh_token_A  (INVALIDADO)
  └─► refresh_token_B  (novo, válido)

Segundo refresh com refresh_token_B
  └─► refresh_token_B  (INVALIDADO)
  └─► refresh_token_C  (novo, válido)

Tentativa de reusar refresh_token_A (já invalidado)
  └─► 400 Bad Request {"error":"invalid_grant"}
      → Keycloak invalida TODA a sessão (proteção contra roubo de token)
```

> **Consequência importante:** Se um `refresh_token` for usado duas vezes (ex.: race condition em cliente multi-tab), o Keycloak invalida a sessão inteira. O cliente deve serializar as chamadas de refresh.

---

## Pseudocódigo — Implementação no Cliente

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
    expiresAt    = now() + response.expires_in - 30s  // margem de 30s

  isExpired():
    return now() >= expiresAt

  refresh():
    response = POST /token (grant_type=refresh_token, refresh_token=refreshToken)
    if response.error == "invalid_grant":
      triggerReLogin()   // sessão expirada — novo login obrigatório
    else:
      store(response)

  getAccessToken():
    if isExpired():
      refresh()          // proativo
    return accessToken

  request(url, method):
    token = getAccessToken()
    response = HTTP(url, Authorization: Bearer token)
    if response.status == 401:
      refresh()          // reativo (fallback)
      token = getAccessToken()
      response = HTTP(url, Authorization: Bearer token)
    return response
```

---

## Troubleshooting

| Problema | Causa provável | Solução |
|----------|---------------|---------|
| `{"error":"invalid_grant"}` no refresh | `refresh_token` expirado ou já usado | Redirecionar para novo login |
| `{"error":"invalid_grant"}` inesperado | Race condition — token usado duas vezes | Serializar chamadas de refresh; armazenar o mais recente |
| Kong retorna `401` com token recém-renovado | `ClockSkew` — descompasso de relógio entre servidores | As APIs toleram 30s de skew (`ClockSkew = TimeSpan.FromSeconds(30)`) |
| `{"message":"Unauthorized"}` do Kong | `access_token` expirado ou malformado | Verificar `exp` do token em [jwt.io](https://jwt.io); fazer refresh |
| Refresh retorna `400` em cliente confidencial | `client_secret` ausente ou incorreto | Incluir `client_secret` no body do request de refresh |
| `{"error":"unauthorized_client"}` | `grant_type=refresh_token` não permitido para o client | Verificar `standardFlowEnabled: true` no client do Keycloak |

---

## Resumo dos Endpoints Keycloak

Todos os endpoints de token estão disponíveis via OIDC Discovery:

```bash
curl -s http://localhost:8081/realms/fincontrol/.well-known/openid-configuration \
  | jq '{token_endpoint, end_session_endpoint, introspection_endpoint}'
```

| Operação | Método | Endpoint |
|----------|--------|----------|
| Login / Obter token | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/token` |
| Refresh token | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/token` |
| Logout | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/logout` |
| Introspecção | POST | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/token/introspect` |
| JWKS (chave pública) | GET | `http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs` |

---

## Referências

- [KONG_KEYCLOAK_OIDC.md](KONG_KEYCLOAK_OIDC.md) — configuração JWT no Kong
- [KONG_KEYCLOAK_TESTS.md](KONG_KEYCLOAK_TESTS.md) — testes de integração
- [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md) — configuração do realm
- [RFC 6749 — OAuth 2.0](https://datatracker.ietf.org/doc/html/rfc6749#section-6) — Refresh Token Grant
- [Keycloak — Session and Token Timeouts](https://www.keycloak.org/docs/latest/server_admin/#_timeouts)

---

**Versão:** 1.0
**Última atualização:** Maio 2026
**Status:** Ativo
