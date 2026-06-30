# Initialization Sequence - FinControl Backend

## Overview

The infrastructure uses **three initialization containers** that run configuration scripts and exit. They should not remain running after successfully completing.

```
vault-init      → creates secrets in Vault
keycloak-init   → creates realm, clients, roles and users in Keycloak
kong-init       → configures services, routes, key-auth and OIDC in Kong
```

Each one depends on the previous via `depends_on: service_completed_successfully`.

---

## Complete Sequence

```
docker-compose up -d
        |
        v
[PHASE 1 - Infrastructure]
  fincontrol-postgres    (5-10s)   → healthcheck: pg_isready
  fincontrol-redis       (3-5s)    → healthcheck: redis-cli ping
  fincontrol-rabbitmq    (5-10s)   → healthcheck: rabbitmq-diagnostics ping
  fincontrol-vault       (15-20s)  → healthcheck: /v1/sys/health
  fincontrol-jaeger      (5-10s)   → healthcheck: wget /
  fincontrol-prometheus  (10-15s)  → healthcheck: wget /-/healthy
        |
        v
[PHASE 2 - Platform]
  fincontrol-keycloak    (20-30s)  → healthcheck: TCP :8080 open
  fincontrol-kong        (15-25s)  → healthcheck: TCP :8001 open
  fincontrol-grafana     (10-20s)  → healthcheck: /api/health
        |
        v
[PHASE 3 - Automatic Initialization]
  fincontrol-vault-init     (5-10s)  → creates secret/dev/* in Vault
        |
        v
  fincontrol-keycloak-init  (5-10s)  → creates realm fincontrol, clients, roles, users
        |
        v
  fincontrol-kong-init      (5-10s)  → registers services, routes, key-auth, OIDC
        |
        v
  Exited (0) x3  → INFRASTRUCTURE READY
```

**Estimated total time:** 90-120 seconds

---

## Timing per Container

| Container | Startup Time | Expected Status |
|-----------|-----------------|-----------------|
| fincontrol-postgres | 5-10s | healthy |
| fincontrol-redis | 3-5s | healthy |
| fincontrol-rabbitmq | 5-10s | healthy |
| fincontrol-vault | 15-20s | healthy |
| fincontrol-jaeger | 5-10s | healthy |
| fincontrol-prometheus | 10-15s | healthy |
| fincontrol-keycloak | 20-30s | healthy |
| fincontrol-kong | 15-25s | healthy |
| fincontrol-grafana | 10-20s | healthy |
| fincontrol-vault-init | 5-10s | **Exited (0)** |
| fincontrol-keycloak-init | 5-10s | **Exited (0)** |
| fincontrol-kong-init | 5-10s | **Exited (0)** |

---

## Status Monitoring

```powershell
# View all containers
docker-compose ps

# Monitor init logs in real time
docker-compose logs -f vault-init
docker-compose logs -f keycloak-init
docker-compose logs -f kong-init
```

### Success Signals

**vault-init:**
```
All secrets have been successfully initialized!
```

**keycloak-init:**
```
Keycloak successfully initialized!
```

**kong-init:**
```
Kong OIDC successfully configured!
```

---

## Initialization Containers Cleanup

Init containers end with status `Exited (0)` after completion. They can be removed at any time.

### Via script

```powershell
.\scripts\Cleanup-Init-Containers.ps1
```

### Manually

```powershell
docker rm -f fincontrol-vault-init fincontrol-keycloak-init fincontrol-kong-init
```

### Via docker-compose

```powershell
docker-compose rm -f vault-init keycloak-init kong-init
```

---

## Permanent vs. Temporary Containers

### DO NOT REMOVE:
- `fincontrol-postgres` — main database
- `fincontrol-redis` — distributed cache
- `fincontrol-rabbitmq` — message broker
- `fincontrol-vault` — secrets manager
- `fincontrol-keycloak` — identity provider
- `fincontrol-kong` — API gateway
- `fincontrol-prometheus` / `fincontrol-grafana` — observability
- `fincontrol-jaeger` — distributed tracing

### SAFE TO REMOVE (after Exited 0):
- `fincontrol-vault-init`
- `fincontrol-keycloak-init`
- `fincontrol-kong-init`

---

## Expected State After Initialization

```
NAME                       IMAGE                    STATUS
fincontrol-postgres        postgres:16-alpine       Up (healthy)
fincontrol-redis           redis:7-alpine           Up (healthy)
fincontrol-rabbitmq        rabbitmq:3.12-mgmt       Up (healthy)
fincontrol-vault           hashicorp/vault:1.15     Up (healthy)
fincontrol-keycloak        keycloak/keycloak        Up (healthy)
fincontrol-kong            kong:3.4                 Up (healthy)
fincontrol-prometheus      prom/prometheus          Up (healthy)
fincontrol-grafana         grafana/grafana          Up (healthy)
fincontrol-jaeger          jaegertracing/all-in-one Up (unhealthy*)

(* Jaeger healthcheck via wget can be flaky — does not prevent operation)
```

---

## Troubleshooting

### vault-init failed

```powershell
docker-compose logs vault-init

# Common causes:
# 1. Vault still initializing — wait another 20s
# 2. Vault did not pass healthcheck — verify docker-compose ps vault

# Re-run:
docker-compose run --rm vault-init
```

### keycloak-init failed

```powershell
docker-compose logs keycloak-init

# Common causes:
# 1. Keycloak still initializing (can take 30-60s)
# 2. vault-init did not complete (keycloak-init depends on it)
# 3. Keycloak database not initialized

# Re-run:
docker-compose run --rm keycloak-init
```

### kong-init failed

```powershell
docker-compose logs kong-init

# Common causes:
# 1. keycloak-init did not complete
# 2. Kong still running bootstrap migrations
# 3. Keycloak client secret not saved in Vault

# Re-run:
docker-compose run --rm kong-init
```

### "Container does not exist" when trying to remove

Normal — the container has already been removed previously. Check with:
```bash
docker ps -a | grep fincontrol.*init
```

---

## Next Steps

After all inits with `Exited (0)`:

1. Check service health: `docker-compose ps`
2. Confirm secrets in Vault: http://localhost:8200/ui
3. Confirm realm in Keycloak: http://localhost:8081
4. Confirm routes in Kong: http://localhost:8002
5. Start .NET APIs (migrations applied automatically)

---

## References

- [VAULT-INITIALIZATION.md](VAULT-INITIALIZATION.md)
- [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md)
- [KONG_KEYCLOAK_OIDC.md](KONG_KEYCLOAK_OIDC.md)
- [DOCKER-COMPOSE-EXECUTION-GUIDE.md](DOCKER-COMPOSE-EXECUTION-GUIDE.md)

---

**Version:** 2.0
**Last updated:** May 2026
**Status:** Active
