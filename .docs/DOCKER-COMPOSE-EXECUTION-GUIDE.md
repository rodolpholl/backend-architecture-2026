# Docker Compose - FinControl Backend

Infraestrutura completa para desenvolvimento local com PostgreSQL, Redis, RabbitMQ, Vault, Keycloak, Kong, Jaeger, Prometheus e Grafana.

## Servicos

| Servico | Imagem | Porta | Credenciais |
|---------|--------|-------|-------------|
| **PostgreSQL** | `postgres:17-alpine` | 5432 | fincontrol_admin / fincontrol_dev_password_123 |
| **Redis** | `redis:7.4-alpine` | 6379 | password: fincontrol_redis_password_123 |
| **RabbitMQ** | `rabbitmq:3.13-management-alpine` | 5672 / 15672 | fincontrol_user / fincontrol_rabbitmq_password_123 |
| **Vault** | `hashicorp/vault:1.18` | 8200 | Token: fincontrol_dev_token_12345 |
| **Keycloak** | `keycloak/keycloak:latest` | 8081 | admin / fincontrol_keycloak_password_123 |
| **Kong** | `kong:3.8` | 8000 / 8001 / 8002 | — (Admin API sem auth em dev) |
| **Jaeger** | `jaegertracing/all-in-one:latest` | 16686 | — |
| **Loki** | `grafana/loki:3.3.0` | 3100 | — |
| **Prometheus** | `prom/prometheus:latest` | 9090 | — |
| **Grafana** | `grafana/grafana:11.4.0` | 3000 | admin / fincontrol_grafana_password_123 |

## Quick Start

### 1. Iniciar Todos os Servicos

```bash
docker-compose up -d
```

Aguarde ~120 segundos para todos os servicos estabilizarem e os containers de init concluirem.

### 2. Verificar Status

```bash
docker-compose ps
```

Os containers de infra devem estar `healthy`. Os containers de init (`vault-init`, `keycloak-init`, `kong-init`) devem estar `Exited (0)`.

### 3. Acessar as UIs

- **Vault:** http://localhost:8200/ui — Token: `fincontrol_dev_token_12345`
- **Keycloak:** http://localhost:8081 — admin / fincontrol_keycloak_password_123
- **RabbitMQ:** http://localhost:15672 — fincontrol_user / fincontrol_rabbitmq_password_123
- **Kong Manager:** http://localhost:8002
- **Prometheus:** http://localhost:9090
- **Jaeger:** http://localhost:16686
- **Loki:** http://localhost:3100 — (API interna; acesso via Grafana)
- **Grafana:** http://localhost:3000 — admin / fincontrol_grafana_password_123

### 4. Iniciar as APIs .NET

Com a infra rodando, inicie os projetos .NET (migrations aplicadas automaticamente no startup):

```bash
# API de Lancamentos
dotnet run --project src/Modules/Lancamentos/FinControl.Lancamentos.API

# Worker de Consolidados
dotnet run --project src/Modules/Consolidados/FinControl.Consolidado.Worker

# API de Consolidados
dotnet run --project src/Modules/Consolidados/FinControl.Consolidado.API
```

Cada API aplica migrations pendentes automaticamente ao iniciar. Se o Vault nao estiver disponivel, a API falha com erro explicito.

## Comandos Uteis

```bash
# Parar todos os servicos
docker-compose down

# Remover volumes e dados (CUIDADO: apaga banco, cache, etc.)
docker-compose down -v

# Ver logs de um servico
docker-compose logs -f postgres
docker-compose logs -f rabbitmq
docker-compose logs -f vault
docker-compose logs -f vault-init
docker-compose logs -f keycloak-init
docker-compose logs -f kong-init

# Ver status
docker-compose ps

# Reiniciar um servico especifico
docker-compose restart kong
```

## Connection Strings

```
PostgreSQL:  Host=localhost;Port=5432;Database=fincontrol_lancamentos;Username=fincontrol_admin;Password=fincontrol_dev_password_123
Redis:       localhost:6379,password=fincontrol_redis_password_123,abortConnect=false
RabbitMQ:   amqp://fincontrol_user:fincontrol_rabbitmq_password_123@localhost:5672/%2Ffincontrol
Vault:       http://localhost:8200
Keycloak:    http://localhost:8081
OTLP gRPC:  http://localhost:4317
Jaeger UI:  http://localhost:16686
```

Essas connection strings sao injetadas automaticamente pelo Vault via `vault-init`. As APIs as consomem via `VaultKeys.*` — nunca via appsettings diretamente.

## Sequencia de Inicializacao

```
docker-compose up -d
        |
        v
  PostgreSQL, Redis, RabbitMQ, Vault, Jaeger, Prometheus, Loki  (Fase 1 — infra)
        |
        v
  Keycloak, Kong, Grafana                                       (Fase 2 — plataforma)
        |
        v
  vault-init     → cria secrets em secret/dev/*             (Exit 0)
  keycloak-init  → cria realm fincontrol, clients, roles    (Exit 0)
  kong-init      → configura rotas, OIDC e key-auth         (Exit 0)
        |
        v
  Infraestrutura pronta para as APIs .NET
```

Ver [INIT-CONTAINERS-CLEANUP.md](INIT-CONTAINERS-CLEANUP.md) para detalhes sobre os containers de init.

## Arquivos de Inicializacao

```
docker-init/
├── postgres/
│   └── init-databases.sql        (cria databases: fincontrol_lancamentos, keycloak, kong)
├── rabbitmq/
│   ├── rabbitmq.conf
│   └── definitions.json
├── vault/
│   └── init-vault.sh             (cria secrets: postgres, redis, rabbitmq, grafana, keycloak, kong, vault)
├── keycloak/
│   └── keycloak-init.sh          (cria realm fincontrol, clients, usuarios de teste)
├── kong/
│   └── kong-init.sh              (configura services, routes, key-auth e OIDC)
├── prometheus/
│   └── prometheus.yml
└── grafana/
    └── provisioning/
```

## Seguranca

- Secrets nunca ficam em appsettings ou variaveis de ambiente das APIs — sempre via Vault
- `.env.docker` ignorado pelo Git (arquivo local apenas)
- Credenciais acima sao APENAS para desenvolvimento local

## Troubleshooting

**Servico nao inicia:**
```bash
docker-compose logs -f <nome_servico>
docker-compose rm -f <nome_servico> && docker-compose up <nome_servico>
```

**Porta em uso (Windows):**
```powershell
netstat -ano | findstr :5432
```

**Vault nao inicializado (secrets ausentes):**
```bash
# Verificar logs do vault-init
docker-compose logs vault-init

# Re-executar vault-init manualmente
docker-compose run --rm vault-init
```

**Keycloak nao inicializado (realm ausente):**
```bash
docker-compose logs keycloak-init
```

**API .NET falha com "Secret not found":**
Confirmar que `vault-init` completou com exit 0 antes de subir as APIs.

---

**Versao:** 2.0
**Ultima atualizacao:** Maio 2026
**Status:** Ativo
