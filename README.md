# FinControl — Sistema de Controle de Fluxo de Caixa

**Desafio Arquiteto de Software 2026 — v3.0**  
ASP.NET Core 10 | Wolverine 5.39 | Vertical Slice + CQRS + Event-Driven

---

## Documentação

| Arquivo | Conteúdo |
|---|---|
| [desafio-arquiteto-software.pdf](.docs/desafio-arquiteto-software.pdf) | Requisitos do desafio |
| [ARQUITETURA.md](.docs/ARQUITETURA.md) | Decisões técnicas, stack, volumetria |
| [CONVENCOES.md](.docs/CONVENCOES.md) | Premissas e convenções do projeto |
| [PROGRESSO.md](.docs/PROGRESSO.md) | Status e roadmap |
| [REFRESH_TOKEN_FLOW.md](.docs/REFRESH_TOKEN_FLOW.md) | Ciclo de vida dos tokens JWT e fluxo de refresh |

---

## Visão Geral

O sistema é composto por dois serviços independentes:

| Serviço | Responsabilidade | Porta |
|---|---|---|
| **Lancamentos.API** | Registro de débitos e créditos (escrita) | `5083` |
| **Consolidado.API** | Consulta de saldo diário consolidado (leitura) | `5260` |
| **Consolidado.Worker** | Processamento assíncrono dos eventos de lançamento | — |

Os dois serviços são desacoplados via RabbitMQ: `Lancamentos.API` publica eventos; `Consolidado.Worker` consome e atualiza o Redis. Assim, a queda do serviço consolidado não afeta o serviço de lançamentos.

---

## Quick Start

### 1. Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (com WSL 2 no Windows)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### 2. Infraestrutura (Docker)

```bash
# Sobe todos os serviços de infraestrutura
docker-compose up -d

# Aguarda a inicialização (Kong, Keycloak e Vault levam ~30s)
# O kong-init e vault-init são executados automaticamente como init containers
```

Serviços levantados:

| Serviço | URL |
|---|---|
| Kong Proxy | `http://localhost:8000` |
| Kong Manager | `http://localhost:8002` |
| Keycloak Admin | `http://localhost:8081/admin` |
| Vault UI | `http://localhost:8200/ui` |
| RabbitMQ Management | `http://localhost:15672` |
| Grafana | `http://localhost:3000` |
| Jaeger UI | `http://localhost:16686` |
| Prometheus | `http://localhost:9090` |
| Scalar (Lançamentos) | `http://localhost:5083/scalar/v1` |
| Scalar (Consolidado) | `http://localhost:5260/scalar/v1` |

### 3. Build e Testes

```bash
# Build da solução completa
dotnet build

# Todos os testes (83 testes: 48 Lancamentos + 35 Consolidado)
dotnet test

# Apenas os testes de Lançamentos
dotnet test src/tests/FinControl.Lancamentos.Tests/

# Apenas os testes de Consolidado
dotnet test src/tests/FinControl.Consolidado.Tests/
```

### 4. Stress Test (NBomber)

Executa os dois cenários em paralelo (requer infraestrutura rodando):

```bash
dotnet run --project src/tests/FinControl.StressTests/
```

Cenários:
- **Consolidado**: ramp 20s → 50 req/s sustentado → ramp-down 10s · p95 < 500ms · erro < 5%
- **Lançamentos**: ramp 20s → 10 req/s sustentado → ramp-down 10s · p95 < 1000ms · erro < 5%

Relatórios HTML e Markdown são gerados em `stress-reports/` (não versionado).

### 5. Executar as APIs (desenvolvimento local)

```bash
# API de Lançamentos (porta 5083)
dotnet run --project src/Modules/Lancamentos/FinControl.Lancamentos.API/FinControl.Lancamentos.API.csproj

# API de Consolidado (porta 5260)
dotnet run --project src/Modules/Consolidados/FinControl.Consolidado.API/FinControl.Consolidado.API.csproj

# Worker de Consolidado
dotnet run --project src/Modules/Consolidados/FinControl.Consolidado.Worker/FinControl.Consolidado.Worker.csproj
```

---

## Endpoints

### API de Lançamentos — `POST /lancamentos/registrar`

Registra um novo débito ou crédito.

**Via Kong (recomendado):**
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
  "descricao": "Pagamento fornecedor",
  "dataLancamento": "2026-05-23"
}
```

Modalidades: `1 = Credito`, `2 = Debito`, `3 = Outros` (descrição obrigatória)  
Valor em centavos (`15000` = R$ 150,00)

**Resposta `200 OK`:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "Credito",
  "valorFormatado": "R$ 150,00",
  "dataLancamento": "2026-05-23T00:00:00Z",
  "correlationId": "abc-123"
}
```

---

### API de Consolidado — `GET /consolidados/saldo`

Retorna o saldo consolidado do dia. NFR: 50 req/s com no máximo 5% de perda.

**Via Kong (recomendado):**
```
GET http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-23
Authorization: Bearer <jwt-token>
X-Subscription-Key: fc-cons-dev-subkey-2026-xyz789ab
```

**Resposta `200 OK`:**
```json
{
  "data": "2026-05-23",
  "saldoEmCentavos": 45000,
  "saldoFormatado": "R$ 450,00"
}
```

---

### Health Checks (sem passar pelo Kong)

```
GET http://localhost:5083/health        # Lancamentos liveness
GET http://localhost:5083/health/ready  # Lancamentos readiness
GET http://localhost:5260/health        # Consolidado liveness
GET http://localhost:5260/health/ready  # Consolidado readiness
```

---

## Autenticação

O sistema usa dois fatores de autenticação no Kong:

1. **Subscription Key** (`X-Subscription-Key`) — identifica o consumer no API Gateway
2. **JWT Bearer** (`Authorization: Bearer <token>`) — token emitido pelo Keycloak

### Obter token JWT (Keycloak)

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

Credenciais de desenvolvimento:

| Recurso | Usuário | Senha |
|---|---|---|
| Keycloak Admin | `admin` | `fincontrol_keycloak_password_123` |
| RabbitMQ | `fincontrol_user` | `fincontrol_rabbitmq_password_123` |
| Grafana | `admin` | `fincontrol_grafana_password_123` |
| Vault Token | — | `fincontrol_dev_token_12345` |

> **Atenção:** Todos os valores acima são exclusivos para desenvolvimento local.

---

## Estrutura do Projeto

```
src/
├── bulding-blocks/
│   ├── FinControl.SharedKernel/        # Domain primitives (AggregateRoot, DomainEvent, ValueObject, Result<T>)
│   ├── FinControl.Infrastructure/     # Cross-cutting: Vault, Redis, RabbitMQ, Observabilidade, Polly
│   └── FinControl.Auth/               # Keycloak JWT Bearer (AddFinControlKeycloakAuth)
├── Modules/
│   ├── Lancamentos/
│   │   ├── FinControl.Lancamentos.Core/   # Domínio + Features (Vertical Slice) + Outbox + Migrations
│   │   └── FinControl.Lancamentos.API/    # Host + Middleware + Scalar UI
│   └── Consolidados/
│       ├── FinControl.Consolidado.Core/   # Domínio + Features (Vertical Slice)
│       ├── FinControl.Consolidado.API/    # Host + Middleware + Scalar UI
│       └── FinControl.Consolidado.Worker/ # Consumer RabbitMQ (reconexão exponencial 5s→60s)
└── tests/
    ├── FinControl.Lancamentos.Tests/  # xUnit + Moq + Bogus — 48 testes
    ├── FinControl.Consolidado.Tests/  # xUnit + Moq + Bogus — 35 testes
    └── FinControl.StressTests/        # NBomber 5.5.0 — execução manual (dotnet run)

docker-init/
├── kong/kong-init.sh      # Provisiona services, routes e plugins no Kong
├── vault/init-vault.sh    # Inicializa secrets no HashiCorp Vault
└── keycloak/              # Realm, clients e usuários iniciais
```

---

## Stack

| Categoria | Tecnologia |
|---|---|
| Runtime | .NET 10 / ASP.NET Core 10 (Minimal APIs) |
| Mediator + Bus | Wolverine 5.39 (CQRS, HTTP, middleware pipeline) |
| Banco de Dados | PostgreSQL 16 + EF Core 10 (Npgsql) |
| Cache + Lock | Redis 7 — `RedisCacheService` + `IRedisLockService` (SETNX + Lua) |
| Mensageria | RabbitMQ 3.12 — Outbox Pattern + `OutboxRelayService` (Polly retry 3×) |
| API Gateway | Kong Gateway OSS — JWT RS256, rate-limiting, request-transformer |
| Identidade | Keycloak 26 (OIDC / JWT Bearer) |
| Secrets | HashiCorp Vault 1.15 (KV v2 — `VaultConfigurationProvider`) |
| Resiliência | Polly v8 (`ResiliencePipelineBuilder` — backoff exponencial + jitter) |
| Tracing | OpenTelemetry + Jaeger (OTLP) |
| Métricas | Prometheus (`prometheus-net`) + Grafana 11 |
| Logs | Serilog + Grafana Loki (CorrelationId enriquecido) |
| Documentação API | Scalar UI (OpenAPI — ambas as APIs) |
| Testes unitários | xUnit + Moq + Bogus + FluentAssertions (83 testes) |
| Stress Test | NBomber 5.5.0 — relatórios HTML + Markdown |
| Padrão | Vertical Slice + CQRS + Event-Driven + DDD |
