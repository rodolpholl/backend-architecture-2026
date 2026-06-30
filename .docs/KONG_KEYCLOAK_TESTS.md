# Kong + Keycloak — Testes de Integração

> **Objetivo:** Verificar que Kong está protegendo as APIs corretamente via JWT RS256 + rate-limiting.
> **Audiência:** Desenvolvedores e SREs

## Pré-requisitos

- `docker-compose up -d` executado e todos os inits com `Exited (0)`
- Kong respondendo: `http://localhost:8001/status` (HTTP 200)
- Keycloak respondendo: `http://localhost:8081/health/ready` (HTTP 200)
- Realm `fincontrol` e clientes configurados (ver [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md))

---

## Teste 1: Verificar Services e Routes no Kong

**Objetivo:** Confirmar que kong-init criou os services e routes com as URLs corretas.

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

**Resultado esperado:**
```
fincontrol-lancamentos  →  http://host.docker.internal:5083
fincontrol-consolidados →  http://host.docker.internal:5260
```

PASS → Services com URLs corretas (`5083` / `5260`)
FAIL → Ver `docker-compose logs kong-init`

---

## Teste 2: Verificar Plugins Ativos

**Objetivo:** Confirmar que `jwt`, `request-transformer`, `rate-limiting` e `proxy-cache` estão habilitados.

**Bash:**
```bash
curl -s http://localhost:8001/plugins | jq '.data[] | {name, enabled}'
```

**Resultado esperado:**
```json
{"name": "jwt",                 "enabled": true}
{"name": "request-transformer", "enabled": true}
{"name": "rate-limiting",       "enabled": true}
{"name": "proxy-cache",         "enabled": true}
```

---

## Teste 3: Obter Token JWT do Keycloak

**Objetivo:** Autenticar com Keycloak e obter JWT RS256.

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

PASS → Token JWT retornado (string longa começando com `eyJ...`)
FAIL → Verificar credenciais em [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md)

---

## Teste 4: Acesso Sem Token JWT (deve ser bloqueado)

**Objetivo:** Confirmar que Kong rejeita requests sem `Authorization: Bearer`.

**Bash:**
```bash
curl -s -o /dev/null -w "%{http_code}" \
  http://localhost:8000/lancamentos/health
# Esperado: 401
```

PASS → HTTP 401 com `{"message":"Unauthorized"}`
FAIL → Plugin `jwt` não está ativo — ver Teste 2

---

## Teste 5: Acesso Com Token JWT Válido (deve passar)

**Objetivo:** Confirmar que Kong encaminha requests com JWT válido.

**PowerShell:**
```powershell
$response = Invoke-WebRequest -Uri "http://localhost:8000/lancamentos/health" `
    -Headers @{"Authorization" = "Bearer $TOKEN"}
Write-Host "Status: $($response.StatusCode)"
```

**Bash:**
```bash
curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:8000/lancamentos/health
# Esperado: 200
```

PASS → HTTP 200
FAIL → Verificar token e configuração do plugin `jwt` no Kong

---

## Teste 6: Token JWT Inválido (deve ser rejeitado)

**Bash:**
```bash
curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiJ9.invalid.invalid" \
  http://localhost:8000/lancamentos/health
# Esperado: 401
```

PASS → HTTP 401
FAIL → Plugin `jwt` não está validando assinatura

---

## Teste 7: JWKS do Keycloak (chave pública RS256)

**Objetivo:** Confirmar que a chave pública usada para validar tokens está disponível.

**Bash:**
```bash
curl -s http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs | \
  jq '.keys[] | {kty, alg, use, kid}'
```

**Resultado esperado:**
```json
{
  "kty": "RSA",
  "alg": "RS256",
  "use": "sig",
  "kid": "..."
}
```

---

## Teste 8: OIDC Discovery do Keycloak

**Objetivo:** Confirmar endpoints do Keycloak.

**Bash:**
```bash
curl -s http://localhost:8081/realms/fincontrol/.well-known/openid-configuration | \
  jq '{issuer, token_endpoint, jwks_uri}'
```

**Resultado esperado:**
```json
{
  "issuer": "http://localhost:8081/realms/fincontrol",
  "token_endpoint": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token",
  "jwks_uri": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs"
}
```

---

## Teste 9: Rate Limiting (Consolidado — 55 req/s)

**Objetivo:** Confirmar que o rate limiting está ativo nos headers de resposta.

**Bash:**
```bash
curl -s -I \
  -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-23" \
  | grep -i "ratelimit"
# Esperado: X-RateLimit-Limit-Second: 55
```

---

## Report Template

```
# Kong + Keycloak Integration Test Report
Data: [DATA]

| Teste | Status | Detalhes |
|-------|--------|----------|
| 1. Services/Routes (portas 5083/5260)  | PASS/FAIL | |
| 2. Plugins ativos (jwt + rate-limiting)| PASS/FAIL | |
| 3. Token Keycloak RS256                | PASS/FAIL | |
| 4. Sem JWT → bloqueado                 | PASS/FAIL | HTTP 401 esperado |
| 5. Com JWT válido → passa              | PASS/FAIL | HTTP 200 esperado |
| 6. JWT inválido → bloqueado            | PASS/FAIL | HTTP 401 esperado |
| 7. JWKS disponível                     | PASS/FAIL | |
| 8. OIDC Discovery                      | PASS/FAIL | |
| 9. Rate limiting nos headers           | PASS/FAIL | |
```

---

## Troubleshooting

| Problema | Solução |
|----------|---------|
| `Connection refused` em :8000 | Kong não subiu — `docker-compose ps kong` |
| `Unauthorized` com token válido | Verificar issuer — deve ser `http://localhost:8081/realms/fincontrol` |
| Token expirado | Renovar token (TTL padrão: 300s) |
| Services com portas erradas | `docker-compose logs kong-init` — verificar se usa `5083`/`5260` |
| `502 Bad Gateway` | API .NET não está rodando — iniciar as APIs (ver [../README.md](../README.md)) |
| kong-init não completou | `docker-compose logs kong-init` |

---

**Versão:** 3.0
**Última atualização:** Maio 2026
**Status:** Ativo
