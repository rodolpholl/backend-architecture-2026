# Kong + Keycloak — Integração JWT RS256

> **Objetivo:** Descrever como o Kong autentica requisições usando JWT RS256 emitido pelo Keycloak.
> **Status:** Configuração automática via `kong-init`

---

## Visão Geral

O Kong valida tokens JWT usando a **chave pública RS256 do Keycloak**, registrada como credencial no plugin `jwt` nativo do Kong. Nenhum plugin OIDC é necessário.

Além disso, o Kong injeta o header `X-Subscription-Key` automaticamente via `request-transformer`, que é então validado pelo `SubscriptionKeyMiddleware` dentro de cada API .NET.

```
Cliente
    |
    | GET /consolidados/saldo  +  Authorization: Bearer <JWT RS256>
    v
Kong Gateway (:8000)
    |-- 1. jwt plugin:               valida assinatura RS256 com chave pública do Keycloak
    |-- 2. request-transformer:      injeta X-Subscription-Key no header upstream
    |-- 3. rate-limiting:            300 req/min (Lançamentos) | 55 req/s (Consolidado)
    |-- 4. proxy-cache:              cache GET /consolidados por 30s
    |
    v (se JWT válido)
FinControl.Lancamentos.API (:5083)  ou  FinControl.Consolidado.API (:5260)
    |
    v
SubscriptionKeyMiddleware (segunda camada de validação interna)
```

---

## Plugins Kong Configurados

| Plugin | Escopo | Configuração |
|--------|--------|--------------|
| `jwt` | Global | RS256; `key_claim_name = iss`; chave pública do Keycloak registrada por consumer |
| `request-transformer` | Por service | Injeta `X-Subscription-Key` no upstream |
| `rate-limiting` | Por route | 300 req/min (Lançamentos) · 55 req/s (Consolidado) |
| `proxy-cache` | Consolidado | Cache GET por 30s; estratégia `cache-control` |

---

## Services e Routes

| Service | URL interna (Docker) | Route path |
|---------|----------------------|-----------|
| `fincontrol-lancamentos` | `http://host.docker.internal:5083` | `/lancamentos` |
| `fincontrol-consolidados` | `http://host.docker.internal:5260` | `/consolidados` |

---

## Como o JWT Plugin Funciona com Keycloak

O Kong `jwt` plugin valida o token sem chamar o Keycloak em runtime — usa a chave pública pre-registrada:

```
Keycloak emite JWT (RS256)
    └─ Header: { "alg": "RS256", "kid": "..." }
    └─ Claims: { "iss": "http://localhost:8081/realms/fincontrol", "sub": "...", ... }

Kong recebe o token:
    1. Lê o claim configurado em key_claim_name (padrão: iss)
    2. Busca consumer com credencial JWT correspondente ao issuer
    3. Valida assinatura com a chave pública registrada
    4. Se válido → encaminha ao upstream
    5. Se inválido → HTTP 401
```

A chave pública do Keycloak é obtida via:
```bash
curl -s http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs | jq '.keys[0]'
```

---

## Quick Start

### 1. Iniciar infraestrutura

```bash
docker-compose up -d
```

Aguardar todos os init containers terminarem com `Exited (0)`.

### 2. Verificar que Kong está configurado

```bash
# Listar services
curl -s http://localhost:8001/services | jq '.data[].name'

# Listar plugins ativos
curl -s http://localhost:8001/plugins | jq '.data[] | {name, enabled}'
```

Resultado esperado:
```json
{"name": "jwt",                "enabled": true}
{"name": "request-transformer","enabled": true}
{"name": "rate-limiting",      "enabled": true}
{"name": "proxy-cache",        "enabled": true}
```

### 3. Obter token JWT do Keycloak

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

### 4. Fazer requisição autenticada

```bash
# Lançamentos
curl -s -w "\nHTTP %{http_code}\n" \
  http://localhost:8000/lancamentos/health \
  -H "Authorization: Bearer $TOKEN"

# Consolidado
curl -s -w "\nHTTP %{http_code}\n" \
  "http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-23" \
  -H "Authorization: Bearer $TOKEN"
```

---

## Configuração Manual (se kong-init falhar)

### Criar Service — Lançamentos

```bash
curl -X POST http://localhost:8001/services \
  -d name=fincontrol-lancamentos \
  -d url=http://host.docker.internal:5083
```

### Criar Service — Consolidado

```bash
curl -X POST http://localhost:8001/services \
  -d name=fincontrol-consolidados \
  -d url=http://host.docker.internal:5260
```

### Criar Routes

```bash
curl -X POST http://localhost:8001/services/fincontrol-lancamentos/routes \
  -d name=lancamentos-route \
  -d "paths[]=/lancamentos"

curl -X POST http://localhost:8001/services/fincontrol-consolidados/routes \
  -d name=consolidados-route \
  -d "paths[]=/consolidados"
```

### Habilitar JWT Plugin

```bash
curl -X POST http://localhost:8001/plugins \
  -d name=jwt \
  -d "config.claims_to_verify[]=exp" \
  -d "config.key_claim_name=iss"
```

### Criar Consumer com credencial JWT (chave pública do Keycloak)

```bash
# Criar consumer
curl -X POST http://localhost:8001/consumers \
  -d username=fincontrol-keycloak-consumer

# Registrar chave pública RS256
# (substitua RSA_PUBLIC_KEY pela chave pública obtida do Keycloak JWKS)
curl -X POST http://localhost:8001/consumers/fincontrol-keycloak-consumer/jwt \
  -d algorithm=RS256 \
  -d "key=http://localhost:8081/realms/fincontrol" \
  -d "rsa_public_key=<RSA_PUBLIC_KEY>"
```

### Habilitar Rate Limiting

```bash
# Lançamentos — 300 req/min
curl -X POST http://localhost:8001/services/fincontrol-lancamentos/plugins \
  -d name=rate-limiting \
  -d "config.minute=300" \
  -d "config.policy=local"

# Consolidado — 55 req/s (3300 req/min)
curl -X POST http://localhost:8001/services/fincontrol-consolidados/plugins \
  -d name=rate-limiting \
  -d "config.second=55" \
  -d "config.policy=local"
```

### Habilitar Proxy Cache (Consolidado)

```bash
curl -X POST http://localhost:8001/services/fincontrol-consolidados/plugins \
  -d name=proxy-cache \
  -d "config.response_code[]=200" \
  -d "config.request_method[]=GET" \
  -d "config.cache_ttl=30"
```

---

## Troubleshooting

| Problema | Solução |
|----------|---------|
| `No API key found in request` | O JWT está ausente ou malformado no header `Authorization` |
| `Invalid signature` | Chave pública registrada no Kong não corresponde ao issuer do token |
| `Unauthorized` (401) | Token expirado (TTL: 300s) — use o `refresh_token` para renovar (ver [REFRESH_TOKEN_FLOW.md](REFRESH_TOKEN_FLOW.md)) |
| `403 Rate limit exceeded` | Aguarde ou ajuste `config.minute`/`config.second` no plugin |
| kong-init falhou | `docker-compose logs kong-init` para diagnóstico |
| JWKS não disponível | Verificar se Keycloak está healthy: `http://localhost:8081/health/ready` |

---

## Referências

- [Kong JWT Plugin](https://docs.konghq.com/hub/kong-inc/jwt/)
- [Kong Rate Limiting Plugin](https://docs.konghq.com/hub/kong-inc/rate-limiting/)
- [Kong Proxy Cache Plugin](https://docs.konghq.com/hub/kong-inc/proxy-cache/)
- [Keycloak JWKS](https://www.keycloak.org/docs/latest/securing_apps/#_certificate_endpoint)
- [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md)
- [KONG_KEYCLOAK_TESTS.md](KONG_KEYCLOAK_TESTS.md)
- [REFRESH_TOKEN_FLOW.md](REFRESH_TOKEN_FLOW.md) — ciclo de vida dos tokens e fluxo de refresh no cliente

---

**Versão:** 3.0
**Última atualização:** Maio 2026
**Status:** Ativo — configuração automatizada via kong-init
