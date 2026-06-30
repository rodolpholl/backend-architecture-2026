# Progresso — FinControl Backend

> **Objetivo:** Status de implementação e roadmap do projeto.
> **Audiência:** Avaliadores do desafio e contribuidores
> **Última atualização:** Maio 2026

---

## Conformidade com o Desafio

| Requisito | Status | Evidência |
|-----------|--------|-----------|
| Serviço de controle de lançamentos | ✅ | `POST /lancamentos/registrar` — Lancamentos.API |
| Serviço de consolidado diário | ✅ | `GET /consolidados/saldo` — Consolidado.API |
| Desenho da solução | ✅ | [ARQUITETURA.md](ARQUITETURA.md) + diagrama `.docs/*.drawio.png` |
| Implementado em C# | ✅ | .NET 10 / ASP.NET Core 10 |
| Testes automatizados | ✅ | 83 testes (xUnit + Moq + Bogus + FluentAssertions) |
| Boas práticas (SOLID, Design Patterns, Arquitetura) | ✅ | Vertical Slice, CQRS, DDD, Outbox, Idempotência |
| README com instruções de execução | ✅ | [../README.md](../README.md) |
| Repositório público (GitHub) | ✅ | Disponível no GitHub |
| Todas as documentações no repositório | ✅ | Pasta `.docs/` |

### Requisitos Não-Funcionais

| Requisito | Status | Como atendido |
|-----------|--------|---------------|
| Lançamentos não cai se Consolidado cair | ✅ | Serviços desacoplados via RabbitMQ; Outbox Pattern garante entrega |
| 50 req/s no Consolidado com ≤5% de perda | ✅ | Redis cache (99%+ hit rate); Stress Test NBomber valida o SLA |

---

## Status de Implementação

### O que está implementado e funcional

| Componente | Observações |
|-----------|-------------|
| **Lancamentos.API** | `POST /lancamentos/registrar` — Wolverine handlers, Vault, JWT, Idempotência |
| **Lancamentos.Core** | CQRS via Wolverine, FluentValidation, EF Core 10, Outbox manual |
| **Consolidado.API** | `GET /consolidados/saldo?data-lancamento=yyyy-MM-dd` via Redis |
| **Consolidado.Core** | Command + Query handlers via Wolverine |
| **Consolidado.Worker** | Consumer RabbitMQ direto → atualiza Redis com lock distribuído |
| **Infrastructure** | Cache, Lock distribuído (SETNX + Lua), Vault, Middleware, RabbitMqPublisher, Polly |
| **SharedKernel** | Entidades base, eventos, Result<T> tipado |
| **Auth** | Integração Keycloak (JWT Bearer RS256 via `AddFinControlKeycloakAuth`) |
| **HashiCorp Vault** | Secrets carregados via `VaultConfigurationProvider`; APIs falham explicitamente se Vault indisponível |
| **Redis** | `RedisCacheService` + `IRedisLockService` (SETNX + Lua release atômico) |
| **Outbox Pattern** | `OutboxMessage` no PostgreSQL + `OutboxRelayService` (polling 5s, batch 50) |
| **Polly** | Retry 3× exponencial com jitter no OutboxRelayService |
| **RabbitMqPublisher** | Publisher AMQP reutilizável (building block) |
| **PostgreSQL + Migrations** | Auto-apply no startup (fail-fast); idempotência com `gen_random_uuid()` |
| **Idempotência** | `IdempotencyKey` (UUID) + índice único no BD |
| **Soft Delete** | Global query filter `DeletedAt == null` |
| **SubscriptionKeyMiddleware** | Segunda camada após Kong; bypass `/health` e `/metrics`; timing-safe |
| **Kong JWT RS256** | Chave pública Keycloak registrada como credencial JWT no Kong |
| **Kong rate-limiting** | 300 req/min (Lançamentos) · 55 req/s (Consolidado) |
| **Kong proxy-cache** | Cache GET Consolidado por 30s |
| **Kong request-transformer** | Injeta `X-Subscription-Key` automaticamente no upstream |
| **Grafana Dashboard** | Dashboard HTTP provisionado via JSON (`fincontrol-http-v1`) |
| **Prometheus /metrics** | `prometheus-net` expõe métricas nas duas APIs |
| **OpenTelemetry** | Traces exportados via OTLP → Jaeger |
| **Serilog** | Logs estruturados + Grafana Loki + CorrelationId enriquecido |
| **Testes unitários** | 83 testes: 48 (Lançamentos) + 35 (Consolidado), zero falhas |
| **Testes funcionais (regras de negócio)** | 12 testes em `ConsolidadoRegrasDenegocioTests` cobrindo crédito, débito, fallback, retroativo e precisão monetária |
| **Stress Tests** | NBomber 5.5.0 — 50 req/s (Consolidado) + 10 req/s (Lançamentos) em paralelo; JWT auto-fetch do Keycloak; relatórios HTML + Markdown |

---

## Roadmap — Em Aberto

Itens identificados durante o desenvolvimento, adiados por escopo ou impacto em refatoração maior:

| Item | Motivo adiado |
|------|--------------|
| `totalCreditos`/`totalDebitos` no saldo consolidado | Redesign do modelo `SaldoConsolidado` (hoje só armazena `Saldo` + `UltimaAtualizacao`) |
| `Lancamento` herdar `AggregateRoot<long>` | Refatoração de escopo maior; impacto no mapeamento EF |
| Deduplicação de `ModalidadeLancamento` (enum duplicado em dois assemblies) | Impacto em cast `(ModalidadeLancamento)(int)` no consumer |
| Setters públicos → encapsulamento em `Lancamento` | Modelo anêmico — refatoração de domínio pendente |

---

## Evoluções Futuras (além do desafio)

O desafio incentiva documentar o que seria implementado dado mais tempo. Aqui estão as evoluções mais relevantes:

### 1. Extrato de Lançamentos (`GET /lancamentos/extrato`)
Endpoint de consulta paginada de lançamentos por período, com filtros por modalidade e ordenação por data. Exigiria `ReadModel` separado no módulo Lançamentos.

### 2. Deduplicação e DLQ com retry manual
Consumer da DLQ com painel Grafana dedicado e endpoint de reprocessamento manual. Hoje a DLQ existe mas não há mecanismo de replay via API.

### 3. Testes de Integração com Testcontainers
Os testes atuais são unitários com mocks. Testes de integração reais usando `Testcontainers` (PostgreSQL + Redis) dariam mais confiança nos fluxos completos, especialmente no Outbox e no lock distribuído.

### 4. Migração de Consolidado para microsserviço autônomo
O `Consolidado.Worker` e `Consolidado.API` compartilham infraestrutura hoje. Em produção real, cada um seria um deploy independente com configuração própria de Vault, Redis e escalabilidade.

### 5. Multi-tenancy
Isolar dados por comerciante com `TenantId` filtrado globalmente no EF Core, propagado via JWT claim → `ClaimsPrincipal` → `ICurrentTenant`.

### 6. Auditoria Imutável (Event Store)
Usar Marten ou EventStoreDB para persistir todos os eventos de domínio imutavelmente, possibilitando replay e auditoria financeira completa.

### 7. gRPC interno para comunicação Worker → Core
Substituir a comunicação via Redis direto por gRPC interno, com schema Protobuf versionado, para comunicação mais robusta entre serviços.

### 8. Circuit Breaker no Kong para fallback
Configurar o plugin `response-ratelimiting` do Kong com fallback de resposta cacheada quando Consolidado estiver degradado, reduzindo erros percebidos pelo cliente.

---

**Versão:** 1.0
**Status:** Ativo
**Última atualização:** Maio 2026
