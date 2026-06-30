# Docker Compose - FinControl Backend

Complete infrastructure for local development with PostgreSQL, Redis, RabbitMQ, Vault, Keycloak, Kong, Jaeger, Prometheus and Grafana.

## Services

| Service | Image | Port | Credentials |
|---------|--------|-------|-------------|
| **PostgreSQL** | `postgres:17-alpine` | 5432 | fincontrol_admin / fincontrol_dev_password_123 |
| **Redis** | `redis:7.4-alpine` | 6379 | password: fincontrol_redis_password_123 |
| **RabbitMQ** | `rabbitmq:3.13-management-alpine` | 5672 / 15672 | fincontrol_user / fincontrol_rabbitmq_password_123 |
| **Vault** | `hashicorp/vault:1.18` | 8200 | Token: fincontrol_dev_token_12345 |
| **Keycloak** | `keycloak/keycloak:latest` | 8081 | admin / fincontrol_keycloak_password_123 |
| **Kong** | `kong:3.8` | 8000 / 8001 / 8002 | — (Admin API without auth in dev) |
| **Jaeger** | `jaegertracing/all-in-one:latest` | 16686 | — |
| **Loki** | `grafana/loki:3.3.0` | 3100 | — |
| **Prometheus** | `prom/prometheus:latest` | 9090 | — |
| **Grafana** | `grafana/grafana:11.4.0` | 3000 | admin / fincontrol_grafana_password_123 |

## Quick Start

### 1. Start All Services

```bash
docker-compose up -d
```

Wait ~120 seconds for all services to stabilize and init containers to complete.

### 2. Check Status

```bash
docker-compose ps
```

Infrastructure containers should be `healthy`. Init containers (`vault-init`, `keycloak-init`, `kong-init`) should be `Exited (0)`.

### 3. Access UIs

- **Vault:** http://localhost:8200/ui — Token: `fincontrol_dev_token_12345`
- **Keycloak:** http://localhost:8081 — admin / fincontrol_keycloak_password_123
- **RabbitMQ:** http://localhost:15672 — fincontrol_user / fincontrol_rabbitmq_password_123
- **Kong Manager:** http://localhost:8002
- **Prometheus:** http://localhost:9090
- **Jaeger:** http://localhost:16686
- **Loki:** http://localhost:3100 — (Internal API; access via Grafana)
- **Grafana:** http://localhost:3000 — admin / fincontrol_grafana_password_123

### 4. Start .NET APIs

With infrastructure running, start .NET projects (migrations applied automatically on startup):

```bash
# Entries API
dotnet run --project src/Modules/Entries/FinControl.Entries.API

# Consolidations Worker
dotnet run --project src/Modules/Consolidations/FinControl.Consolidation.Worker

# Consolidations API
dotnet run --project src/Modules/Consolidations/FinControl.Consolidation.API
```

Each API applies pending migrations automatically on startup. If Vault is unavailable, the API fails with an explicit error.

## Useful Commands

```bash
# Stop all services
docker-compose down

# Remove volumes and data (WARNING: deletes database, cache, etc.)
docker-compose down -v

# View logs for a service
docker-compose logs -f postgres
docker-compose logs -f rabbitmq
docker-compose logs -f vault
docker-compose logs -f vault-init
docker-compose logs -f keycloak-init
docker-compose logs -f kong-init

# Check status
docker-compose ps

# Restart a specific service
docker-compose restart kong
```

## Connection Strings

```
PostgreSQL:  Host=localhost;Port=5432;Database=fincontrol_Entries;Username=fincontrol_admin;Password=fincontrol_dev_password_123
Redis:       localhost:6379,password=fincontrol_redis_password_123,abortConnect=false
RabbitMQ:   amqp://fincontrol_user:fincontrol_rabbitmq_password_123@localhost:5672/%2Ffincontrol
Vault:       http://localhost:8200
Keycloak:    http://localhost:8081
OTLP gRPC:  http://localhost:4317
Jaeger UI:  http://localhost:16686
```

These connection strings are injected automatically by Vault via `vault-init`. APIs consume them via `VaultKeys.*` — never directly via appsettings.

## Initialization Sequence

```
docker-compose up -d
        |
        v
  PostgreSQL, Redis, RabbitMQ, Vault, Jaeger, Prometheus, Loki  (Phase 1 — infrastructure)
        |
        v
  Keycloak, Kong, Grafana                                       (Phase 2 — platform)
        |
        v
  vault-init     → creates secrets in secret/dev/*             (Exit 0)
  keycloak-init  → creates realm fincontrol, clients, roles    (Exit 0)
  kong-init      → configures routes, OIDC and key-auth         (Exit 0)
        |
        v
  Infrastructure ready for .NET APIs
```

See [INIT-CONTAINERS-CLEANUP.md](INIT-CONTAINERS-CLEANUP.md) for details on init containers.

## Initialization Files

```
docker-init/
├── postgres/
│   └── init-databases.sql        (creates databases: fincontrol_Entries, keycloak, kong)
├── rabbitmq/
│   ├── rabbitmq.conf
│   └── definitions.json
├── vault/
│   └── init-vault.sh             (creates secrets: postgres, redis, rabbitmq, grafana, keycloak, kong, vault)
├── keycloak/
│   └── keycloak-init.sh          (creates realm fincontrol, clients, test users)
├── kong/
│   └── kong-init.sh              (configures services, routes, key-auth and OIDC)
├── prometheus/
│   └── prometheus.yml
└── grafana/
    └── provisioning/
```

## Security

- Secrets never in appsettings or environment variables of APIs — always via Vault
- `.env.docker` ignored by Git (local file only)
- Credentials above are ONLY for local development

## Troubleshooting

**Service does not start:**
```bash
docker-compose logs -f <service_name>
docker-compose rm -f <service_name> && docker-compose up <service_name>
```

**Port in use (Windows):**
```powershell
netstat -ano | findstr :5432
```

**Vault not initialized (missing secrets):**
```bash
# Check vault-init logs
docker-compose logs vault-init

# Re-run vault-init manually
docker-compose run --rm vault-init
```

**Keycloak not initialized (realm missing):**
```bash
docker-compose logs keycloak-init
```

**.NET API fails with "Secret not found":**
Confirm that `vault-init` completed with exit 0 before starting APIs.

---

**Version:** 2.0
**Last updated:** May 2026
**Status:** Active

