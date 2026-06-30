# Progress — FinControl Backend

> **Objective:** Implementation status and project roadmap.
> **Audience:** Challenge evaluators and contributors
> **Last updated:** May 2026

---

## Challenge Compliance

| Requirement | Status | Evidence |
|-----------|--------|-----------|
| Lancamentos (entry) control service | ✅ | `POST /lancamentos/registrar` — Lancamentos.API |
| Daily consolidated service | ✅ | `GET /consolidados/saldo` — Consolidado.API |
| Solution design | ✅ | [ARCHITECTURE.md](ARCHITECTURE.md) + diagram `.docs/*.drawio.png` |
| Implemented in C# | ✅ | .NET 10 / ASP.NET Core 10 |
| Automated tests | ✅ | 83 tests (xUnit + Moq + Bogus + FluentAssertions) |
| Best practices (SOLID, Design Patterns, Architecture) | ✅ | Vertical Slice, CQRS, DDD, Outbox, Idempotency |
| README with execution instructions | ✅ | [../README.md](../README.md) |
| Public repository (GitHub) | ✅ | Available on GitHub |
| All documentation in repository | ✅ | `.docs/` folder |

### Non-Functional Requirements

| Requirement | Status | How met |
|-----------|--------|---------------|
| Lancamentos does not crash if Consolidado crashes | ✅ | Services decoupled via RabbitMQ; Outbox Pattern guarantees delivery |
| 50 req/s on Consolidado with ≤5% loss | ✅ | Redis cache (99%+ hit rate); NBomber Stress Test validates SLA |

---

## Implementation Status

### What is implemented and functional

| Component | Observations |
|-----------|-------------|
| **Lancamentos.API** | `POST /lancamentos/registrar` — Wolverine handlers, Vault, JWT, Idempotency |
| **Lancamentos.Core** | CQRS via Wolverine, FluentValidation, EF Core 10, manual Outbox |
| **Consolidado.API** | `GET /consolidados/saldo?data-lancamento=yyyy-MM-dd` via Redis |
| **Consolidado.Core** | Command + Query handlers via Wolverine |
| **Consolidado.Worker** | RabbitMQ consumer → updates Redis with distributed lock |
| **Infrastructure** | Cache, Distributed Lock (SETNX + Lua), Vault, Middleware, RabbitMqPublisher, Polly |
| **SharedKernel** | Base entities, events, typed Result<T> |
| **Auth** | Keycloak integration (JWT Bearer RS256 via `AddFinControlKeycloakAuth`) |
| **HashiCorp Vault** | Secrets loaded via `VaultConfigurationProvider`; APIs fail explicitly if Vault unavailable |
| **Redis** | `RedisCacheService` + `IRedisLockService` (SETNX + atomic Lua release) |
| **Outbox Pattern** | `OutboxMessage` in PostgreSQL + `OutboxRelayService` (5s polling, batch 50) |
| **Polly** | 3× exponential retry with jitter on OutboxRelayService |
| **RabbitMqPublisher** | Reusable AMQP publisher (building block) |
| **PostgreSQL + Migrations** | Auto-apply on startup (fail-fast); idempotency with `gen_random_uuid()` |
| **Idempotency** | `IdempotencyKey` (UUID) + unique index in database |
| **Soft Delete** | Global query filter `DeletedAt == null` |
| **SubscriptionKeyMiddleware** | Second layer after Kong; bypasses `/health` and `/metrics`; timing-safe |
| **Kong JWT RS256** | Keycloak public key registered as JWT credential in Kong |
| **Kong rate-limiting** | 300 req/min (Lancamentos) · 55 req/s (Consolidado) |
| **Kong proxy-cache** | Cache GET Consolidado for 30s |
| **Kong request-transformer** | Injects `X-Subscription-Key` automatically upstream |
| **Grafana Dashboard** | HTTP Dashboard provisioned via JSON (`fincontrol-http-v1`) |
| **Prometheus /metrics** | `prometheus-net` exposes metrics on both APIs |
| **OpenTelemetry** | Traces exported via OTLP → Jaeger |
| **Serilog** | Structured logs + Grafana Loki + enriched CorrelationId |
| **Unit tests** | 83 tests: 48 (Lancamentos) + 35 (Consolidado), zero failures |
| **Functional tests (business rules)** | 12 tests in `ConsolidadoRegrasDenegocioTests` covering credits, debits, fallback, retroactive, and monetary precision |
| **Stress Tests** | NBomber 5.5.0 — 50 req/s (Consolidado) + 10 req/s (Lancamentos) in parallel; auto-fetch JWT from Keycloak; HTML + Markdown reports |

---

## Roadmap — Open Items

Items identified during development, deferred due to scope or impact on larger refactoring:

| Item | Reason deferred |
|------|--------------|
| `totalCreditos`/`totalDebitos` in consolidated balance | `SaldoConsolidado` model redesign (currently only stores `Saldo` + `UltimaAtualizacao`) |
| `Lancamento` inherit `AggregateRoot<long>` | Larger refactoring scope; impact on EF mapping |
| Deduplication of `ModalidadeLancamento` (enum duplicated in two assemblies) | Impact on `(ModalidadeLancamento)(int)` cast in consumer |
| Public setters → encapsulation in `Lancamento` | Anemic model — pending domain refactoring |

---

## Future Evolutions (beyond the challenge)

The challenge encourages documenting what would be implemented given more time. Here are the most relevant evolutions:

### 1. Lancamentos Extract (`GET /lancamentos/extrato`)
Paginated query endpoint for lancamentos by period, with filters by type and date ordering. Would require separate `ReadModel` in Lancamentos module.

### 2. DLQ Deduplication with manual retry
DLQ consumer with dedicated Grafana panel and manual reprocessing endpoint. Today the DLQ exists but there is no replay mechanism via API.

### 3. Integration Tests with Testcontainers
Current tests are unit tests with mocks. Real integration tests using `Testcontainers` (PostgreSQL + Redis) would give more confidence in complete flows, especially in Outbox and distributed lock.

### 4. Migration of Consolidado to autonomous microservice
`Consolidado.Worker` and `Consolidado.API` share infrastructure today. In real production, each would be an independent deployment with its own Vault, Redis, and scalability configuration.

### 5. Multi-tenancy
Isolate data by merchant with `TenantId` filtered globally in EF Core, propagated via JWT claim → `ClaimsPrincipal` → `ICurrentTenant`.

### 6. Immutable Audit (Event Store)
Use Marten or EventStoreDB to immutably persist all domain events, enabling replay and complete financial audit.

### 7. gRPC internal for Worker → Core communication
Replace direct Redis communication with internal gRPC, with versioned Protobuf schema, for more robust inter-service communication.

### 8. Circuit Breaker in Kong for fallback
Configure Kong's `response-ratelimiting` plugin with fallback to cached response when Consolidado is degraded, reducing errors perceived by client.

---

**Version:** 1.0
**Status:** Active
**Last updated:** May 2026
