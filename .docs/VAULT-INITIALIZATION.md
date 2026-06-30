# Vault Initialization Flow

## Overview

O Vault e inicializado automaticamente pelo container `vault-init` apos o Vault ficar saudavel.

Fluxo:
1. **Vault** inicia em modo dev com token fixo `fincontrol_dev_token_12345`
2. **vault-init** aguarda o healthcheck do Vault passar
3. **vault-init** executa `/scripts/init-vault.sh` criando todos os secrets em `secret/dev/*`
4. Container encerra com exit 0

Em seguida, `keycloak-init` aguarda `vault-init` completar antes de configurar o Keycloak.

---

## Secrets Criados

| Secret Path | Chaves | Consumidor |
|-------------|--------|------------|
| `secret/dev/postgres` | `connection_string`, username, password, host, port | APIs .NET via `VaultKeys.PostgresConnection` |
| `secret/dev/redis` | `connection_string`, password, host, port | APIs .NET via `VaultKeys.RedisConnection` |
| `secret/dev/rabbitmq` | `uri`, username, password, vhost, host, port | APIs .NET via `VaultKeys.RabbitMqUri` |
| `secret/dev/grafana` | `loki_url`, `otlp_endpoint`, `prometheus_pushgateway` | APIs .NET via `VaultKeys.LokiUrl` etc. |
| `secret/dev/keycloak` | `realm`, `url`, `issuer`, `jwks_uri`, `kong_client_id`, `kong_client_secret`, `api_client_id`, `api_client_secret` | APIs .NET via `VaultKeys.Keycloak*` |
| `secret/dev/kong` | `lancamentos_subscription_key`, `consolidados_subscription_key` | APIs .NET via `VaultKeys.Kong*SubscriptionKey` |
| `secret/dev/vault` | `root_token` | Metadados internos |

### Como as chaves sao lidas no .NET

O `VaultConfigurationProvider` usa o ultimo segmento do path como namespace:

```
secret/dev/postgres → key "connection_string"
    => IConfiguration["postgres:connection_string"]

secret/dev/keycloak → key "realm"
    => IConfiguration["keycloak:realm"]
```

Acesse sempre via `builder.Configuration[VaultKeys.CONSTANTE]`.

---

## Detalhes do Container vault-init

```yaml
vault-init:
  image: alpine:3.19
  container_name: fincontrol-vault-init
  depends_on:
    vault:
      condition: service_healthy
  environment:
    VAULT_ADDR: http://vault:8200
    VAULT_TOKEN: fincontrol_dev_token_12345
    VAULT_ENV: dev
  entrypoint: /bin/sh
  command: |
    apk add --no-cache bash curl
    /bin/bash /scripts/init-vault.sh
  restart: no
```

O script usa `bash` (nao `sh`) por precisar de:
- `${VAR:-default}` — parameter expansion
- `set -euo pipefail` — bash strict mode
- `local -a array=("$@")` — declaracao de array

---

## Como Verificar

### 1. Vault UI

```
http://localhost:8200/ui
Token: fincontrol_dev_token_12345
```

Navegue para `secret > dev >` — voce deve ver: `grafana`, `keycloak`, `kong`, `postgres`, `rabbitmq`, `redis`, `vault`.

### 2. Via CLI

```bash
# Entrar no container vault
docker-compose exec vault sh

# Ler um secret
vault kv get secret/dev/postgres
vault kv get secret/dev/keycloak
vault kv get secret/dev/kong
```

### 3. Ver logs

```bash
docker-compose logs vault-init --tail 50
```

Saida esperada ao final:
```
Todos os secrets foram inicializados com sucesso!
```

---

## Troubleshooting

### vault-init termina com Exited (1)

```bash
# Ver erro detalhado
docker-compose logs vault-init

# Causas comuns:
# 1. Vault ainda inicializando — aguarde mais 20s e tente novamente
# 2. Permissao negada no script — verificar montagem do volume

# Re-executar:
docker-compose rm -f vault-init
docker-compose run --rm vault-init
```

### Secrets nao aparecem na UI

```bash
# Confirmar que vault-init completou com sucesso
docker-compose logs vault-init | grep -E "(sucesso|Error)"

# Verificar conexao
curl -s http://localhost:8200/v1/sys/health | jq .sealed
# Deve retornar: false
```

### API .NET falha com "Secret not found"

A API lanca excecao explicita ao subir se qualquer secret obrigatorio nao estiver no Vault. Verifique:

1. `docker-compose ps vault-init` — deve mostrar `Exited (0)`
2. `docker-compose logs vault-init | tail -5` — deve mostrar sucesso
3. Confirme que `vault.settings.json` lista os paths corretos (`dev/postgres`, `dev/redis`, etc.)

---

## Proximas Etapas Pos-Inicializacao

1. `vault-init` completa → `keycloak-init` inicia automaticamente
2. `keycloak-init` completa → `kong-init` inicia automaticamente
3. Todos os inits com `Exited (0)` → infraestrutura pronta para as APIs .NET

Ver [INIT-CONTAINERS-CLEANUP.md](INIT-CONTAINERS-CLEANUP.md) para remover os containers de init apos conclusao.

---

**Versao:** 2.0
**Ultima atualizacao:** Maio 2026
**Status:** Ativo
