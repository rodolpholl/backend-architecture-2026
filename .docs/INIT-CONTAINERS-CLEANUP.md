# Sequencia de Inicializacao - FinControl Backend

## Visao Geral

A infraestrutura usa **tres containers de inicializacao** que executam scripts de configuracao e encerram. Eles nao devem permanecer em execucao apos completar com sucesso.

```
vault-init      → cria secrets no Vault
keycloak-init   → cria realm, clients, roles e usuarios no Keycloak
kong-init       → configura services, routes, key-auth e OIDC no Kong
```

Cada um depende do anterior via `depends_on: service_completed_successfully`.

---

## Sequencia Completa

```
docker-compose up -d
        |
        v
[FASE 1 - Infra]
  fincontrol-postgres    (5-10s)   → healthcheck: pg_isready
  fincontrol-redis       (3-5s)    → healthcheck: redis-cli ping
  fincontrol-rabbitmq    (5-10s)   → healthcheck: rabbitmq-diagnostics ping
  fincontrol-vault       (15-20s)  → healthcheck: /v1/sys/health
  fincontrol-jaeger      (5-10s)   → healthcheck: wget /
  fincontrol-prometheus  (10-15s)  → healthcheck: wget /-/healthy
        |
        v
[FASE 2 - Plataforma]
  fincontrol-keycloak    (20-30s)  → healthcheck: TCP :8080 aberto
  fincontrol-kong        (15-25s)  → healthcheck: TCP :8001 aberto
  fincontrol-grafana     (10-20s)  → healthcheck: /api/health
        |
        v
[FASE 3 - Inicializacao Automatica]
  fincontrol-vault-init     (5-10s)  → cria secret/dev/* no Vault
        |
        v
  fincontrol-keycloak-init  (5-10s)  → cria realm fincontrol, clients, roles, usuarios
        |
        v
  fincontrol-kong-init      (5-10s)  → registra services, routes, key-auth, OIDC
        |
        v
  Exited (0) x3  → INFRAESTRUTURA PRONTA
```

**Tempo total estimado:** 90-120 segundos

---

## Timing por Container

| Container | Tempo de Startup | Status Esperado |
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

## Monitoramento de Status

```powershell
# Ver todos os containers
docker-compose ps

# Monitorar logs dos inits em tempo real
docker-compose logs -f vault-init
docker-compose logs -f keycloak-init
docker-compose logs -f kong-init
```

### Sinais de Sucesso

**vault-init:**
```
Todos os secrets foram inicializados com sucesso!
```

**keycloak-init:**
```
Keycloak inicializado com sucesso!
```

**kong-init:**
```
Kong OIDC configurado com sucesso!
```

---

## Limpeza dos Containers de Inicializacao

Os containers de init ficam com status `Exited (0)` apos conclusao. Podem ser removidos a qualquer momento.

### Via script

```powershell
.\scripts\Cleanup-Init-Containers.ps1
```

### Manualmente

```powershell
docker rm -f fincontrol-vault-init fincontrol-keycloak-init fincontrol-kong-init
```

### Via docker-compose

```powershell
docker-compose rm -f vault-init keycloak-init kong-init
```

---

## Containers Permanentes vs. Temporarios

### NAO REMOVA:
- `fincontrol-postgres` — banco principal
- `fincontrol-redis` — cache distribuido
- `fincontrol-rabbitmq` — message broker
- `fincontrol-vault` — secrets manager
- `fincontrol-keycloak` — identity provider
- `fincontrol-kong` — API gateway
- `fincontrol-prometheus` / `fincontrol-grafana` — observabilidade
- `fincontrol-jaeger` — tracing distribuido

### SEGURO REMOVER (apos Exited 0):
- `fincontrol-vault-init`
- `fincontrol-keycloak-init`
- `fincontrol-kong-init`

---

## Estado Esperado Pos-Inicializacao

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

(* Jaeger healthcheck via wget pode ser flaky — nao impede o funcionamento)
```

---

## Troubleshooting

### vault-init falhou

```powershell
docker-compose logs vault-init

# Causas comuns:
# 1. Vault ainda inicializando — aguardar mais 20s
# 2. Vault nao passou no healthcheck — verificar docker-compose ps vault

# Reexecutar:
docker-compose run --rm vault-init
```

### keycloak-init falhou

```powershell
docker-compose logs keycloak-init

# Causas comuns:
# 1. Keycloak ainda inicializando (pode demorar 30-60s)
# 2. vault-init nao completou (keycloak-init depende dele)
# 3. DB do Keycloak nao inicializado

# Reexecutar:
docker-compose run --rm keycloak-init
```

### kong-init falhou

```powershell
docker-compose logs kong-init

# Causas comuns:
# 1. keycloak-init nao completou
# 2. Kong ainda executando migrations do bootstrap
# 3. Client secret do Keycloak nao foi salvo no Vault

# Reexecutar:
docker-compose run --rm kong-init
```

### "Container does not exist" ao tentar remover

Normal — o container ja foi removido anteriormente. Verificar com:
```bash
docker ps -a | grep fincontrol.*init
```

---

## Proximos Passos

Apos todos os inits com `Exited (0)`:

1. Verificar saude dos servicos: `docker-compose ps`
2. Confirmar secrets no Vault: http://localhost:8200/ui
3. Confirmar realm no Keycloak: http://localhost:8081
4. Confirmar rotas no Kong: http://localhost:8002
5. Iniciar as APIs .NET (migrations aplicadas automaticamente)

---

## Referencias

- [VAULT-INITIALIZATION.md](VAULT-INITIALIZATION.md)
- [KEYCLOAK_SETUP_GUIDE.md](KEYCLOAK_SETUP_GUIDE.md)
- [KONG_KEYCLOAK_OIDC.md](KONG_KEYCLOAK_OIDC.md)
- [DOCKER-COMPOSE-EXECUTION-GUIDE.md](DOCKER-COMPOSE-EXECUTION-GUIDE.md)

---

**Versao:** 2.0
**Ultima atualizacao:** Maio 2026
**Status:** Ativo
