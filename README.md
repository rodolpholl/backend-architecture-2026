# FinControl — Cash Flow Control System

**Software Architect Challenge 2026 — v3.0**  
ASP.NET Core 10 | Wolverine 5.39 | Vertical Slice + CQRS + Event-Driven

---

## Documentation

| File | Content |
|---|---|
| [desafio-arquiteto-software.pdf](.docs/desafio-arquiteto-software.pdf) | Challenge requirements |
| [ARCHITECTURE.md](.docs/ARCHITECTURE.md) | Technical decisions, stack, volume |
| [CONVENTIONS.md](.docs/CONVENTIONS.md) | Project assumptions and conventions |
| [PROGRESS.md](.docs/PROGRESS.md) | Status and roadmap |
| [REFRESH_TOKEN_FLOW.md](.docs/REFRESH_TOKEN_FLOW.md) | JWT token lifecycle and refresh flow |

---

## Overview

The system consists of two independent services:

| Service | Responsibility | Port |
|---|---|---|
| **Transactions.API** | Registration of debits and credits (write) | `5083` |
| **Consolidated.API** | Query of consolidated daily balance (read) | `5260` |
| **Consolidated.Worker** | Asynchronous processing of transaction events | — |

The two services are decoupled via RabbitMQ: `Transactions.API` publishes events; `Consolidated.Worker` consumes and updates Redis. Thus, the failure of the consolidated service does not affect the transactions service.

---

## Quick Start

### 1. Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (with WSL 2 on Windows)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 2. Infrastructure (Docker)

```bash
# Brings up all infrastructure services
docker-compose up -d

# Wait for initialization (Kong, Keycloak and Vault take ~30s)
# kong-init and vault-init are automatically executed as init containers
```

Services launched:

| Service | URL |
|---|---|
| Kong Proxy | `http://localhost:8000` |
| Kong Manager | `http://localhost:8002` |
| Keycloak Admin | `http://localhost:8081/admin` |
| Vault UI | `http://localhost:8200/ui` |
| RabbitMQ Management | `http://localhost:15672` |
| Grafana | `http://localhost:3000` |
| Jaeger UI | `http://localhost:16686` |
| Prometheus | `http://localhost:9090` |
| Scalar (Transactions) | `http://localhost:5083/scalar/v1` |
| Scalar (Consolidated) | `http://localhost:5260/scalar/v1` |

### 3. Build and Tests

```bash
# Build the complete solution
dotnet build

# All tests (83 tests: 48 Transactions + 35 Consolidated)
dotnet test

# Transactions tests only
dotnet test src/tests/FinControl.Lancamentos.Tests/

# Consolidated tests only
dotnet test src/tests/FinControl.Consolidado.Tests/
```

### 4. Stress Test (NBomber)

Executes both scenarios in parallel (requires infrastructure running):

```bash
dotnet run --project src/tests/FinControl.StressTests/
```

Scenarios:
- **Consolidated**: ramp 20s → 50 req/s sustained → ramp-down 10s · p95 < 500ms · error < 5%
- **Transactions**: ramp 20s → 10 req/s sustained → ramp-down 10s · p95 < 1000ms · error < 5%

HTML and Markdown reports are generated in `stress-reports/` (not versioned).

### 5. Running the APIs (local development)

```bash
# Transactions API (port 5083)
dotnet run --project src/Modules/Lancamentos/FinControl.Lancamentos.API/FinControl.Lancamentos.API.csproj

# Consolidated API (port 5260)
dotnet run --project src/Modules/Consolidados/FinControl.Consolidado.API/FinControl.Consolidado.API.csproj

# Consolidated Worker
dotnet run --project src/Modules/Consolidados/FinControl.Consolidado.Worker/FinControl.Consolidado.Worker.csproj
```

---

## Endpoints

### Transactions API — `POST /lancamentos/registrar`

Registers a new debit or credit.

**Via Kong (recommended):**
```
POST http://localhost:8000/lancamentos/registrar
Authorization: Bearer <jwt-token>
X-Subscription-Key: fc-lanc-dev-subkey-2026-abc123ef
Content-Type: application/json
```

**Body:**
```json
{
  "modalidade": 1,
  "valor": 15000,
  "descricao": "Supplier payment",
  "dataLancamento": "2026-05-23"
}
```

Categories: `1 = Credit`, `2 = Debit`, `3 = Others` (description required)  
Amount in cents (`15000` = R$ 150.00)

**Response `200 OK`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "Credit",
  "valorFormatado": "R$ 150.00",
  "dataLancamento": "2026-05-23T00:00:00Z",
  "correlationId": "abc-123"
}
```

---

### Consolidated API — `GET /consolidados/saldo`

Returns the consolidated balance for the day. NFR: 50 req/s with max 5% loss.

**Via Kong (recommended):**
```
GET http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-23
Authorization: Bearer <jwt-token>
X-Subscription-Key: fc-cons-dev-subkey-2026-xyz789ab
```

**Response `200 OK`:**
```json
{
  "data": "2026-05-23",
  "saldoEmCentavos": 45000,
  "saldoFormatado": "R$ 450.00"
}
```

---

### Health Checks (without passing through Kong)

```
GET http://localhost:5083/health        # Transactions liveness
GET http://localhost:5083/health/ready  # Transactions readiness
GET http://localhost:5260/health        # Consolidated liveness
GET http://localhost:5260/health/ready  # Consolidated readiness
```

---

## Authentication

The system uses two authentication factors in Kong:

1. **Subscription Key** (`X-Subscription-Key`) — identifies the consumer on the API Gateway
2. **JWT Bearer** (`Authorization: Bearer <token>`) — token issued by Keycloak

### Get JWT token (Keycloak)

```bash
curl -s -X POST http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=fincontrol-backend" \
  -d "client_secret=fincontrol-backend-secret-12345" \
  -d "username=admin.fincontrol" \
  -d "password=Admin@123456" \
  | jq -r '.access_token'
```

Development credentials:

| Resource | User | Password |
|---|---|---|
| Keycloak Admin | `admin` | `fincontrol_keycloak_password_123` |
| RabbitMQ | `fincontrol_user` | `fincontrol_rabbitmq_password_123` |
| Grafana | `admin` | `fincontrol_grafana_password_123` |
| Vault Token | — | `fincontrol_dev_token_12345` |

> **Warning:** All values above are exclusive to local development.

---

## Project Structure

```
src/
├── bulding-blocks/
│   ├── FinControl.SharedKernel/        # Domain primitives (AggregateRoot, DomainEvent, ValueObject, Result<T>)
│   ├── FinControl.Infrastructure/     # Cross-cutting: Vault, Redis, RabbitMQ, Observability, Polly
│   └── FinControl.Auth/               # Keycloak JWT Bearer (AddFinControlKeycloakAuth)
├── Modules/
│   ├── Lancamentos/
│   │   ├── FinControl.Lancamentos.Core/   # Domain + Features (Vertical Slice) + Outbox + Migrations
│   │   └── FinControl.Lancamentos.API/    # Host + Middleware + Scalar UI
│   └── Consolidados/
│       ├── FinControl.Consolidado.Core/   # Domain + Features (Vertical Slice)
│       ├── FinControl.Consolidado.API/    # Host + Middleware + Scalar UI
│       └── FinControl.Consolidado.Worker/ # RabbitMQ Consumer (exponential reconnect 5s→60s)
└── tests/
    ├── FinControl.Lancamentos.Tests/  # xUnit + Moq + Bogus — 48 tests
    ├── FinControl.Consolidado.Tests/  # xUnit + Moq + Bogus — 35 tests
    └── FinControl.StressTests/        # NBomber 5.5.0 — manual execution (dotnet run)

docker-init/
├── kong/kong-init.sh      # Provisions services, routes and plugins in Kong
├── vault/init-vault.sh    # Initializes secrets in HashiCorp Vault
└── keycloak/              # Realm, clients and initial users
```

---

## Stack

| Category | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 (Minimal APIs) |
| Mediator + Bus | Wolverine 5.39 (CQRS, HTTP, middleware pipeline) |
| Database | PostgreSQL 16 + EF Core 10 (Npgsql) |
| Cache + Lock | Redis 7 — `RedisCacheService` + `IRedisLockService` (SETNX + Lua) |
| Messaging | RabbitMQ 3.12 — Outbox Pattern + `OutboxRelayService` (Polly retry 3×) |
| API Gateway | Kong Gateway OSS — JWT RS256, rate-limiting, request-transformer |
| Identity | Keycloak 26 (OIDC / JWT Bearer) |
| Secrets | HashiCorp Vault 1.15 (KV v2 — `VaultConfigurationProvider`) |
| Resilience | Polly v8 (`ResiliencePipelineBuilder` — exponential backoff + jitter) |
| Tracing | OpenTelemetry + Jaeger (OTLP) |
| Metrics | Prometheus (`prometheus-net`) + Grafana 11 |
| Logs | Serilog + Grafana Loki (enriched CorrelationId) |
| API Documentation | Scalar UI (OpenAPI — both APIs) |
| Unit Tests | xUnit + Moq + Bogus + FluentAssertions (83 tests) |
| Stress Test | NBomber 5.5.0 — HTML + Markdown reports |
| Pattern | Vertical Slice + CQRS + Event-Driven + DDD |
