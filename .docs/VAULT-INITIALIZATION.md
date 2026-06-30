# Vault Initialization Flow

## Overview

Vault is automatically initialized by the `vault-init` container after Vault becomes healthy.

Flow:
1. **Vault** starts in dev mode with fixed token `fincontrol_dev_token_12345`
2. **vault-init** waits for Vault healthcheck to pass
3. **vault-init** executes `/scripts/init-vault.sh` creating all secrets in `secret/dev/*`
4. Container exits with exit 0

Then, `keycloak-init` waits for `vault-init` to complete before configuring Keycloak.

---

## Created Secrets

| Secret Path | Keys | Consumer |
|-------------|--------|------------|
| `secret/dev/postgres` | `connection_string`, username, password, host, port | .NET APIs via `VaultKeys.PostgresConnection` |
| `secret/dev/redis` | `connection_string`, password, host, port | .NET APIs via `VaultKeys.RedisConnection` |
| `secret/dev/rabbitmq` | `uri`, username, password, vhost, host, port | .NET APIs via `VaultKeys.RabbitMqUri` |
| `secret/dev/grafana` | `loki_url`, `otlp_endpoint`, `prometheus_pushgateway` | .NET APIs via `VaultKeys.LokiUrl` etc. |
| `secret/dev/keycloak` | `realm`, `url`, `issuer`, `jwks_uri`, `kong_client_id`, `kong_client_secret`, `api_client_id`, `api_client_secret` | .NET APIs via `VaultKeys.Keycloak*` |
| `secret/dev/kong` | `lancamentos_subscription_key`, `consolidados_subscription_key` | .NET APIs via `VaultKeys.Kong*SubscriptionKey` |
| `secret/dev/vault` | `root_token` | Internal metadata |

### How keys are read in .NET

The `VaultConfigurationProvider` uses the last segment of the path as a namespace:

```
secret/dev/postgres → key "connection_string"
    => IConfiguration["postgres:connection_string"]

secret/dev/keycloak → key "realm"
    => IConfiguration["keycloak:realm"]
```

Always access via `builder.Configuration[VaultKeys.CONSTANT]`.

---

## vault-init Container Details

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

The script uses `bash` (not `sh`) because it needs:
- `${VAR:-default}` — parameter expansion
- `set -euo pipefail` — bash strict mode
- `local -a array=("$@")` — array declaration

---

## How to Verify

### 1. Vault UI

```
http://localhost:8200/ui
Token: fincontrol_dev_token_12345
```

Navigate to `secret > dev >` — you should see: `grafana`, `keycloak`, `kong`, `postgres`, `rabbitmq`, `redis`, `vault`.

### 2. Via CLI

```bash
# Enter vault container
docker-compose exec vault sh

# Read a secret
vault kv get secret/dev/postgres
vault kv get secret/dev/keycloak
vault kv get secret/dev/kong
```

### 3. Check logs

```bash
docker-compose logs vault-init --tail 50
```

Expected output at the end:
```
All secrets have been successfully initialized!
```

---

## Troubleshooting

### vault-init exits with Exited (1)

```bash
# See detailed error
docker-compose logs vault-init

# Common causes:
# 1. Vault still initializing — wait another 20s and try again
# 2. Permission denied in script — check volume mount

# Re-run:
docker-compose rm -f vault-init
docker-compose run --rm vault-init
```

### Secrets do not appear in UI

```bash
# Confirm vault-init completed successfully
docker-compose logs vault-init | grep -E "(success|Error)"

# Check connection
curl -s http://localhost:8200/v1/sys/health | jq .sealed
# Should return: false
```

### .NET API fails with "Secret not found"

The API throws an explicit exception on startup if any required secret is not in Vault. Verify:

1. `docker-compose ps vault-init` — should show `Exited (0)`
2. `docker-compose logs vault-init | tail -5` — should show success
3. Confirm that `vault.settings.json` lists correct paths (`dev/postgres`, `dev/redis`, etc.)

---

## Next Steps After Initialization

1. `vault-init` completes → `keycloak-init` starts automatically
2. `keycloak-init` completes → `kong-init` starts automatically
3. All inits with `Exited (0)` → infrastructure ready for .NET APIs

See [INIT-CONTAINERS-CLEANUP.md](INIT-CONTAINERS-CLEANUP.md) to remove init containers after completion.

---

**Version:** 2.0
**Last updated:** May 2026
**Status:** Active
