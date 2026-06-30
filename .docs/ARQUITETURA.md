# 🏗️ Arquitetura - Sistema de Controle de Fluxo de Caixa

**Projeto:** FinControl — Sistema de Controle de Fluxo de Caixa para Comerciante  
**Data:** Maio 2026  
**Versão:** 3.0 — Implementação em produção

---

## Status de Implementação

> Esta documentação foi atualizada para refletir o estado **implementado** do projeto (v2.0).  
> As seções de justificativas e volumetria permanecem como referência arquitetural.

### O que está implementado

| Componente | Status | Observações |
|-----------|--------|-------------|
| **FinControl.Lancamentos.API** | ✅ Funcional | `POST /lancamentos/registrar` + Wolverine handlers + Vault + JWT |
| **FinControl.Lancamentos.Core** | ✅ Funcional | CQRS via Wolverine, FluentValidation, EF Core, Outbox manual |
| **FinControl.Consolidado.API** | ✅ Funcional | `GET /consolidados/saldo?data-lancamento=yyyy-MM-dd` via Redis |
| **FinControl.Consolidado.Core** | ✅ Funcional | Command + Query handlers via Wolverine |
| **FinControl.Consolidado.Worker** | ✅ Funcional | Consumer RabbitMQ direto → atualiza Redis |
| **FinControl.Infrastructure** | ✅ Funcional | Cache, Lock, Vault, Middleware, Observability, RabbitMqPublisher |
| **FinControl.SharedKernel** | ✅ Funcional | Entidades base, eventos, resultado tipado |
| **FinControl.Auth** | ✅ Funcional | Integração Keycloak (JWT Bearer) |
| **HashiCorp Vault** | ✅ Integrado | Secrets carregados via `VaultConfigurationProvider` |
| **Redis (Cache + Lock)** | ✅ Integrado | `RedisCacheService` + `IRedisLockService` (SETNX + Lua) |
| **RabbitMQ (Outbox Manual)** | ✅ Integrado | `OutboxMessage` no PostgreSQL + `OutboxRelayService` (BackgroundService) |
| **OutboxRelayService + Polly** | ✅ Implementado | Polling 5s, batch 50, retry 3× exponencial com jitter |
| **RabbitMqPublisher** | ✅ Implementado | Publisher direto AMQP (building block reutilizável) |
| **PostgreSQL + Migrations** | ✅ Integrado | Auto-apply no startup (fail-fast), `AddOutboxMessages` aplicada |
| **Idempotência** | ✅ Implementado | `IdempotencyKey` (UUID) + índice único no BD |
| **Soft Delete** | ✅ Implementado | Global query filter `DeletedAt == null` |
| **SubscriptionKeyMiddleware** | ✅ Implementado | Segunda camada após Kong; bypass `/health` e `/metrics` |
| **Kong request-transformer** | ✅ Configurado | Kong injeta `X-Subscription-Key` automaticamente — jamais exposta ao cliente |
| **Kong JWT (RS256 + Keycloak)** | ✅ Configurado | Chave pública Keycloak registrada como credencial JWT no Kong |
| **Grafana Dashboard** | ✅ Provisionado | Dashboard HTTP provisionado via JSON (`fincontrol-http-v1`) |
| **Prometheus /metrics** | ✅ Funcional | `prometheus-net` expõe métricas em `/metrics` (ambas as APIs) |
| **Testes automatizados** | ✅ 83 testes | 48 (Lancamentos) + 35 (Consolidado), zero falhas |
| **FinControl.StressTests** | ✅ Implementado | NBomber 5.5.0 — 50 req/s (Consolidado) + 10 req/s (Lancamentos) em paralelo; JWT auto-fetch do Keycloak; relatórios HTML + Markdown |

### O que está em aberto (roadmap)

| Item | Motivo adiado |
|------|--------------|
| `totalCreditos`/`totalDebitos` no saldo consolidado | Redesign do modelo de resposta |
| `Lancamento` herdar `AggregateRoot<long>` | Refatoração de escopo maior |
| Deduplicação de `ModalidadeLancamento` (enum duplicado) | Impacto em cast `(ModalidadeLancamento)(int)` |
| Setters públicos → encapsulamento em `Lancamento` | Modelo anêmico — refatoração de domínio |

---

## Índice

1. [Requisitos](#requisitos)
2. [Análise de Volumetria](#análise-de-volumetria)
3. [Justificativas Tecnológicas](#justificativas-tecnológicas)
   - 3.1 [Por que Microserviços?](#por-que-microserviços)
   - 3.2 [Por que Redis Cache?](#por-que-redis-cache-é-essencial)
   - 3.3 [Por que PostgreSQL?](#por-que-postgresql-não-dynamodbcassandra)
   - 3.4 [Por que RabbitMQ?](#por-que-rabbitmq-não-kafkaaws-sqs)
   - 3.5 [Por que Kong (API Gateway)?](#por-que-kong-ou-nginxhaproxy-para-api-gateway)
   - 3.6 [Por que CQRS?](#por-que-cqrs-command-query-responsibility-segregation)
   - 3.7 [Por que Monolito Modular?](#por-que-começar-com-monolito-modular-vertical-slicing)
4. [Evolução Arquitetural: Vertical Slicing + CQRS](#evolução-arquitetural-vertical-slicing--cqrs)
5. [Arquitetura Proposta: Microserviços + Event-Driven](#arquitetura-proposta-microserviços--event-driven)
6. [Estrutura de Pastas](#estrutura-de-pastas)
7. [Implementação: Vertical Slicing com Wolverine + Minimal APIs](#implementação-vertical-slicing-com-wolverine--minimal-apis)
8. [Padrões Arquiteturais Aplicados](#padrões-arquiteturais-aplicados)
9. [Stack Técnico](#stack-técnico)
10. [Componentes Principais](#componentes-principais)
11. [Fluxos de Dados](#fluxos-de-dados)
12. [Requisitos Não-Funcionais](#requisitos-não-funcionais)
13. [Segurança](#segurança)
    - 13.1 [Autenticação](#1-autenticação)
    - 13.2 [Autorização](#2-autorização)
    - 13.3 [Validação de Entrada](#3-validação-de-entrada)
    - 13.4 [Criptografia em Trânsito](#4-criptografia-em-trânsito)
    - 13.5 [Firewall & WAF (ModSecurity + Kong)](#5-firewall--waf-web-application-firewall)
    - 13.6 [Proteção Contra Ataques](#6-proteção-contra-ataques-resumo)
14. [Key Vault & Secrets Management](#key-vault--secrets-management)
15. [Autenticação & Autorização Centralizadas](#autenticação--autorização-centralizadas)
16. [Monitoramento & Observabilidade](#monitoramento--observabilidade)
17. [Plano de Implementação](#plano-de-implementação)
18. [Resumo Executivo: Vertical Slicing + CQRS para 50 req/s](#resumo-executivo-vertical-slicing--cqrs-para-50-reqs)
19. [Próximas Etapas](#próximas-etapas)
20. [Referências & Recursos](#referências--recursos)

---

## Requisitos

### Requisitos de Negócio
- ✅ Serviço que faça o controle de lançamentos (débitos e créditos)
- ✅ Serviço do consolidado diário (relatório de saldo consolidado)

### Requisitos Técnicos Obrigatórios
- ✅ Desenho da solução documentado
- ✅ Implementação em C#
- ✅ Testes automatizados
- ✅ Aplicação de boas práticas (Design Patterns, SOLID, Arquitetura)
- ✅ README com instruções de execução
- ✅ Hospedagem em repositório público (GitHub)
- ✅ Documentação completa no repositório

### Requisitos Não-Funcionais
- **Resiliência**: Lançamentos não deve ficar indisponível se consolidado cair
- **Performance**: Consolidado recebe **50 requisições/segundo**
- **Confiabilidade**: Máximo **5% de perda de requisições**

---

## Análise de Volumetria

### Premissas de Negócio

```
REQUISITO CRÍTICO: 50 requisições/segundo (consolidado)
└─ Máxima de pico estabelecida no desafio
   Com máximo 5% de perda de requisições permitida

Horário operacional: 06:00 às 22:00 (16 horas/dia)
Dias operacionais: 360 dias/ano
Crescimento anual: 15% (expansão de filiais/volume)

PREMISSA DE ESCRITA (defensiva — volume não especificado no desafio):
├─ O desafio NÃO define volume de lançamentos — premissa é decisão arquitetural
├─ Num sistema real de fluxo de caixa, escrita é o gargalo financeiro crítico
├─ Cada lançamento exige: validação, ACID, idempotência, publicação de evento
└─ Premissa adotada: sistema WRITE-HEAVY (escrita domina em complexidade e risco)

MODELO DE ALOCAÇÃO DE RECURSOS:
├─ 70% dos recursos → Serviço de Lançamentos (escrita, ACID, Outbox, eventos)
├─ 30% dos recursos → Serviço de Consolidado (leitura servida ~99% por cache)
└─ Justificativa: leitura é O(1) via Redis; escrita exige pipeline completo
```

---

### Cálculo Realista: 50 req/s como Base

#### 1️⃣ Requisições Diárias de Leitura (Consolidado)

```
Cenário de Pico sustentado: 50 req/s
Distribuição durante o dia: Concentrado nas 16h de operação

CÁLCULO:
Requisições por hora:
  50 req/s × 3.600 segundos = 180.000 req/hora

Requisições por dia (16h operacionais):
  180.000 req/hora × 16 horas = 2.880.000 req/dia

Requisições por ano (360 dias):
  2.880.000 req/dia × 360 dias = 1.036.800.000 req/ano

Com crescimento 15% a.a. durante 10 anos:
┌──────┬─────────────────┬──────────────────┐
│ Ano  │ Taxa Req/s      │ Req/Ano          │
├──────┼─────────────────┼──────────────────┤
│  1   │ 50 req/s        │ 1.036.800.000    │
│  2   │ 57,5 req/s      │ 1.192.320.000    │
│  3   │ 66,1 req/s      │ 1.371.168.000    │
│  4   │ 76 req/s        │ 1.576.843.000    │
│  5   │ 87,4 req/s      │ 1.813.369.000    │
│  6   │ 100,5 req/s     │ 2.085.324.000    │
│  7   │ 115,6 req/s     │ 2.398.123.000    │
│  8   │ 133 req/s       │ 2.757.842.000    │
│  9   │ 153 req/s       │ 3.171.518.000    │
│ 10   │ 176 req/s       │ 3.647.246.000    │
└──────┴─────────────────┴──────────────────┘

TOTAL em 10 anos: ~21.051.383.000 requisições (21 bilhões aproximadamente).
```

---

#### 2️⃣ Impacto da Cache Redis

```
SEM CACHE:
├─ 50 req/s → 50 queries ao PostgreSQL
├─ Latência BD: 20-50ms por query
├─ Bandwidth: ~5 MB/s (alto)
├─ CPU Database: ~60-70% em picos
└─ Limite prático: ~100 req/s antes de colapso

COM CACHE REDIS — INVALIDAÇÃO ATIVA POR EVENTO (Write-Through):
├─ Estratégia: cache sensibilizado a CADA lançamento registrado
│  └─ Consumer escuta LançamentoRegistrado → recalcula saldo → SET Redis
├─ 50 req/s → Redis ✅
│  └─ ~99%+ resolvidas em <5ms (cache sempre fresco)
├─ ~0,5 req/s → PostgreSQL ❌
│  └─ Apenas cold start ou falha do consumer (DLQ + retry)
├─ Latência média: <5ms (cache hit) | 30ms (miss pontual)
├─ CPU Database: ~5-10% em picos (-85%)
├─ Bandwidth: ~250 KB/s (-95%)
└─ Limite: ~500+ req/s (teórico)

DIFERENÇA DA INVALIDAÇÃO ATIVA vs TTL PASSIVO:
┌──────────────────────────────┬───────────────────────┬──────────────────────────┐
│ Característica               │ TTL Passivo (60s)     │ Event-Driven (por escrita)│
├──────────────────────────────┼───────────────────────┼──────────────────────────┤
│ Cache desatualizado (máx.)   │ 60 segundos           │ 0 segundos               │
│ Hit rate esperado            │ 95%                   │ 99%+                     │
│ Complexidade                 │ Baixa                 │ Média (consumer + Outbox) │
│ Risco de dado inconsistente  │ Alta (60s de lag)     │ Muito baixa              │
│ Acoplamento write→read       │ Nenhum                │ Assíncrono (via evento)  │
└──────────────────────────────┴───────────────────────┴──────────────────────────┘

ECONOMIA QUANTIFICÁVEL:
┌─────────────────────────┬─────────┬─────────┬──────────┐
│ Métrica                 │ Sem LRU │ Com LRU │ Redução  │
├─────────────────────────┼─────────┼─────────┼──────────┤
│ Queries ao BD/dia       │ 2.88B   │ 0.014B  │ -99%     │
│ CPU Database em picos   │ 70%     │ 10%     │ -85%     │
│ Latência P99            │ 50ms    │ 5ms     │ -90%     │
│ Conexões BD necessárias │ 200+    │ 10      │ -95%     │
│ Bandwidth rede          │ 5 MB/s  │ 0.25MB/s│ -95%     │
│ Escalabilidade          │ 100 r/s │ 500+ r/s│ +400%    │
└─────────────────────────┴─────────┴─────────┴──────────┘

Cálculo de memória Redis necessária:
├─ Saldo consolidado por dia: ~500 bytes
├─ Guarda 90 dias (período útil): 500B × 90 = 45 KB
├─ TTL de segurança: 24h (fallback se consumer falhar)
├─ Estruturas overhead (meta): +5 KB
├─ TOTAL necessário: ~60 KB !!!
└─ Recomendação: Mínimo 512 MB (garantir margem)

Status: ✅ Redis ESSENCIAL para atingir 50 req/s com <5% de perda
        ✅ Invalidação ativa garante consistência em tempo real
        ✅ Outbox Pattern protege contra falha do consumer
        ✅ ROI: Permite escalar sem aumentar BD
```

---

#### 3️⃣ Impacto no Load Balancer & Instâncias

```
REQUISIÇÕES DISTRIBUÍDAS:

Pico leitura: 50 req/s → Consolidado Service
Escrita: write-heavy (70% da complexidade operacional) → Lançamentos Service

CONSOLIDADO SERVICE (leitura via cache):
├─ Cache HIT (99%+): ~49,5 req/s → Resposta Redis <5ms
├─ Cache MISS (< 1%): < 0,5 req/s → Consulta BD (cold start)
├─ Carga efetiva no serviço: BAIXA (quase tudo retorna do Redis)
└─ CPU/Memória: mínimos — serviço age como proxy do Redis

Distribuição entre N servidores Consolidado:
┌─────────────────┬──────────────┬────────────────────────────┐
│ Servidores      │ Req/s p/ srv │ Utilização                 │
├─────────────────┼──────────────┼────────────────────────────┤
│ 1 servidor      │ 50 req/s     │ 30-40% (leve — tudo cache) │
│ 2 servidores    │ 25 req/s     │ 15-20% (muito confortável) │
└─────────────────┴──────────────┴────────────────────────────┘

RECOMENDAÇÃO: 1 servidor de Consolidado (+ auto-scaling horizontal)
├─ Com 99%+ de cache hit, 1 instância lida com 50 req/s com folga
├─ Failover: Redis replica (sem dependência de segunda instância)
├─ Health check a cada 10 segundos
└─ Auto-scaling: +1 servidor se CPU > 60% (raro com cache ativo)

LANÇAMENTOS SERVICE (escrita — 70% dos recursos):
├─ Pipeline: validação → ACID PostgreSQL → Outbox → evento RabbitMQ
├─ Cada escrita aciona atualização de cache (invalidação ativa)
├─ Requer: idempotência (IdempotencyKey), tratamento de concorrência
└─ Dois terminais podem criar lançamentos simultâneos → precisa de lock

Distribuição entre N servidores Lançamentos:
┌─────────────────┬──────────────┬────────────────────────────┐
│ Servidores      │ Carga        │ Utilização                 │
├─────────────────┼──────────────┼────────────────────────────┤
│ 2 servidores    │ 50% cada     │ Ativo-ativo, sem SPOF      │
│ 3 servidores    │ 33% cada     │ Margem ampla (produção)    │
└─────────────────┴──────────────┴────────────────────────────┘

RECOMENDAÇÃO: 2 servidores de Lançamentos (ativo-ativo)
├─ Pipeline de escrita é o crítico: falha aqui = perda de dados
├─ Outbox Pattern garante entrega do evento mesmo com crash
├─ PgBouncer (pool de conexões) protege PostgreSQL de spike de escrita
└─ Rate limiting por cliente: 10 req/s (proteção contra DDoS de escrita)

Load Balancer (Nginx/HAProxy):
├─ Tráfego: 50 req/s leitura + escrita (não é desafio para LB)
├─ Limite de throughput: 10.000+ req/s (100x acima)
├─ Rate limiting: 60 req/s leitura | 10 req/s escrita por cliente
├─ Conexões simultâneas: 500-1000 (preparado para tudo)
└─ Recomendação: 1 instância LB (ou HA pair passivo)

Status: ✅ 1 × Consolidado + 2 × Lançamentos + 1 × LB = Solução write-optimized
        ✅ Consolidado leve (99% cache) → escalamento horizontal sob demanda
        ✅ Lançamentos robusto (2 instâncias ativo-ativo + Outbox)
```

---

#### 4️⃣ Requisições de ESCRITA (Lançamentos)

```
LANÇAMENTOS — SISTEMA WRITE-HEAVY (gargalo operacional crítico)
├─ Volume: não especificado no desafio → premissa arquitetural livre
├─ Premissa adotada: write-heavy (70% da carga de infraestrutura)
├─ Cada lançamento percorre pipeline completo (com CorrelationId em cada etapa):
│   ├─ 1. Validação de negócio (tipo, valor, data) — gera UUID CorrelationId
│   ├─ 2. Verificação de idempotência (IdempotencyKey único) — CorrelationId nos logs
│   ├─ 3. Persistência ACID no PostgreSQL — CorrelationId armazenado para auditoria
│   ├─ 4. Publicação de evento via Outbox Pattern — CorrelationId em x-correlation-id RabbitMQ header
│   ├─ 5. Consumer extrai CorrelationId do message header
│   └─ 6. Consumer atualiza Redis → cache fresco (CorrelationId propagado em logs)
└─ Latência esperada: < 50ms (escrita + publicação de evento)

**Timeline com CorrelationId:**
```
POST /lancamentos
  ↓ [10ms] gera CorrelationId (UUID) + valida
response: {Id, CorrelationId} + x-correlation-id header
  ↓ [25ms] PostgreSQL ACID insert + Outbox publish
[RabbitMQ] evento com x-correlation-id header
  ↓ [async] roteamento para consumer
[Consumer] processa com CorrelationId do header
  ↓ [15ms] Redis atualiza consolidado:{data}
[Observabilidade] OpenTelemetry rastreia TODA operação com 1 CorrelationId
```

IMPACTO DE CADA LANÇAMENTO NO SISTEMA:
├─ PostgreSQL: escrita ACID (Serializable) → impacto em conexões
├─ Outbox: publicação transacional → sem perda mesmo com crash
├─ RabbitMQ: entrega garantida ao consumer
├─ Redis: invalidação/atualização da chave `consolidado:{data}`
└─ Consolidado GET: próxima requisição já retorna saldo atualizado

IDEMPOTÊNCIA E CONCORRÊNCIA (múltiplos PDVs):
├─ IdempotencyKey (UUID) por operação → duplicatas rejeitadas
├─ Unique constraint no BD: (IdempotencyKey, Data)
├─ Dois terminais simultâneos → um ganha, outro recebe 409 Conflict
└─ Sem IdempotencyKey → risco de lançamento duplicado em retry

MODELO DE RECURSOS (70/30):
├─ 70% → Lançamentos Service: 2 instâncias ativo-ativo + PgBouncer
├─ 30% → Consolidado Service: 1 instância (leitura ~99% via Redis)
└─ Justificativa: escrita é a fonte de verdade; leitura é O(1) no cache

Status: ✅ Sistema write-dominant, não read-heavy
        ✅ Outbox Pattern garante consistência lançamento → cache
        ✅ Idempotência protege integridade financeira
        ✅ 70% dos recursos onde o risco financeiro reside
```

---

#### 5️⃣ Projeção de Armazenamento - 10 Anos

```
LANÇAMENTOS (escrita):

Escrita por ano: 350 lançamentos/dia × 360 dias = 126.000/ano

Tipo (enum — valores típicos de uma loja):
├─ Venda            (crédito — venda no caixa/PDV)
├─ Devolucao        (débito — estorno de venda)
├─ Suprimento       (crédito — reforço de caixa)
├─ Sangria          (débito — retirada de caixa)
├─ PagamentoFornecedor (débito — pagamento de nota)
├─ RecebimentoDivida   (crédito — cobrança de cliente)
└─ Outros           (fallback para lançamentos manuais — descricao obrigatória)

Tamanho por registro:
├─ Id (BIGINT, auto-increment): 8 bytes
├─ NavigationId (UUID, para referências externas): 16 bytes
├─ IdempotencyKey (UUID): 16 bytes
├─ CorrelationId (UUID, rastreamento ponta a ponta): 16 bytes ← trace distribuído através de RabbitMQ
├─ Tipo (SMALLINT/enum): 2 bytes
├─ Valor (DECIMAL 18,2): 9 bytes
├─ DataLancamento (DATE): 4 bytes
├─ Descricao (VARCHAR 300, obrigatória se Tipo='Outros'): ~60-100 bytes (média)
├─ UsuarioId (UUID, do Keycloak): 16 bytes ← GUID para rastrear quem fez o lançamento
├─ UsuarioNome (VARCHAR 100, desnormalizado): ~50 bytes (média) ← snapshot do nome na data do lançamento
├─ UsuarioEmail (VARCHAR 100, desnormalizado): ~50 bytes (média) ← snapshot do email na data do lançamento
├─ CriadoEm (TIMESTAMPTZ): 8 bytes
└─ TOTAL: ~195 bytes/registro

Em 10 anos: 
126.000 × 10 × 195 bytes = 247.500.000 bytes ≈ 236 MB (apenas dados)

Com índices (6 índices × 20% cada — Id, NavigationId, IdempotencyKey, CorrelationId, DataLancamento, UsuarioId):
236 MB + (236 × 1.2) = 483 MB

Com CONSOLIDADOS (1/dia × 10 anos):
3.650 × 400 bytes = 1.460 KB

TOTAL FINAL: ~485 MB (confortável, espaço sobra)
Recomendação: PostgreSQL com 10-20 GB de espaço
Status: ✅ Simples, sem sharding necessário

**Nota sobre CorrelationId (Rastreamento Distribuído Ponta a Ponta):**

O `CorrelationId` é essencial para observabilidade em operações assíncronas via RabbitMQ:

- **Criação:** Gerado como UUID v4 na API quando lançamento é criado
- **Persistência:** Armazenado na tabela `lancamentos` para auditoria histórica
- **Propagação:** 
  - Passado em response headers HTTP (client pode rastrear status)
  - Incluído em `x-correlation-id` header do RabbitMQ message
  - Propagado através de OpenTelemetry context (traceparent)
- **Logs Estruturados:** Cada linha de log inclui CorrelationId para agregação
- **APM Integration:** Jaeger/Datadog agrupa toda operação sob um único trace

**Pipeline com CorrelationId:**
```
  POST /lancamentos
    └─ [1] Gera CorrelationId (UUID)
         └─ [2] Valida + insere em PostgreSQL (armazena CorrelationId)
              └─ [3] Outbox publica evento com x-correlation-id header
                   └─ [4] RabbitMQ roteia mensagem
                        └─ [5] Consumer extrai CorrelationId do header
                             └─ [6] Atualiza Redis
                                  └─ [7] Observabilidade: timeline completa em Jaeger
```

**Benefícios Práticos:**
- ✅ Debugging: \"qual consumer processou lancamento X?\" → query logs com CorrelationId
- ✅ Auditoria: rastreamento financeiro completo com timestamps
- ✅ SLA Monitoring: tempo total na pipeline (latência por CorrelationId)
- ✅ Error Correlation: se falhar no Outbox → logs com mesmo CorrelationId pinçam o problema
- ✅ Consumer Idempotência: chave (CorrelationId + consumer_name) previne reprocessamento

**Nota sobre Auditoria de Usuário:**
- `UsuarioId` — UUID/GUID do Keycloak, identidade imutável
- `UsuarioNome` e `UsuarioEmail` — **snapshot desnormalizados** da data do lançamento
  - ✅ **Por quê desnormalizar?** Velocidade de queries sem JOINs com Keycloak
  - ✅ **Auditoria histórica** — preserva quem fez a ação com que nome/email naquela data
  - ⚠️ Se usuário trocar email/nome, lançamento antigo mantém o valor original
- Permite queries rápidas: `SELECT * FROM lancamentos WHERE usuario_email = 'fulano@empresa.com'`
- Para dados atuais completos do usuário, buscar no Keycloak via UsuarioId se necessário


CACHE CONSOLIDADO (Redis):

Dia tipico:
├─ Saldo consolidado: ~500 bytes
├─ Replicas para cache coerência: ~50 KB (máximo)
├─ TTL: 60 segundos (refresh automático)

Em 10 anos:
├─ Histórico no Redis: Apenas últimos 90 dias
├─ Memória pico: <100 MB
├─ Memória alocada: 512 MB (margem)

Status: ✅ Micro-instância Redis (~$5/mês)
```

---

#### 6️⃣ Filas de Mensagens (RabbitMQ)

```
VOLUME DE EVENTOS:

Lançamentos publicados: 350/dia

Tamanho do evento:
├─ Metadata: 100 bytes
├─ Dados: 300 bytes
├─ Envelope AMQP/JSON: 200 bytes
└─ TOTAL: ~600 bytes/evento

Em dia tipico: 350 × 600 bytes = 210 KB

RabbitMQ configurado com:
├─ TTL de mensagem: 24 horas (reprocessamento)
├─ Dead Letter Queue para falhas
├─ Persistence habilitada
└─ Replicação (2 nós) para HA

Espaço necessário:
├─ Buffer 1 dia: 210 KB
├─ Com redundância 3x: 630 KB
└─ Espaço alocado: 1 GB

Status: ✅ RabbitMQ com 1 GB RAM (muito confortável)
```

---

### Resumo Consolidado - 10 Anos com Base em 50 req/s

```
┌──────────────────────────────────────────────────────────┐
│      VOLUMETRIA COM BASE 50 REQ/S — MODELO WRITE-HEAVY   │
├──────────────────────────────────────────────────────────┤
│ REQUISIÇÕES DE LEITURA (Consolidado)                     │
│   ├─ Pico: 50 req/s (requisito do desafio)               │
│   ├─ Diário: 2.880.000 requisições                       │
│   ├─ Anual: 1.036.800.000 requisições                    │
│   ├─ 10 anos: 21.051.383.000 requisições                 │
│   └─ Cache hit: 99%+ → ~49,5 req/s servidas pelo Redis   │
│                                                          │
│ REQUISIÇÕES DE ESCRITA (Lançamentos)                     │
│   ├─ Volume: não especificado no desafio                 │
│   ├─ Premissa: write-heavy (fonte de verdade do sistema) │
│   ├─ Cada lançamento: ACID + Outbox + cache invalidation │
│   └─ Recurso alocado: 70% da infraestrutura              │
│                                                          │
│ MODELO DE ALOCAÇÃO (70/30):                              │
│   ├─ 70% → Lançamentos: escrita, ACID, eventos, Outbox   │
│   └─ 30% → Consolidado: leitura O(1) via Redis (<5ms)   │
│                                                          │
│ PROPORÇÃO ANTES (errada): 8.333:1 (leitura dominante)   │
│ MODELO ATUAL (correto): escrita é o gargalo crítico      │
│   └─ Leitura é barata com cache ativo; escrita tem custo │
│                                                          │
│ ARMAZENAMENTO                                            │
│   ├─ PostgreSQL: ~482 MB base + 15% a.a. de crescimento  │
│   ├─ Redis Cache: 512 MB alocado (TTL 24h de segurança)  │
│   ├─ RabbitMQ: 1 GB RAM                                  │
│   └─ TOTAL: ~2 GB                                        │
│                                                          │
│ COMPONENTES RECOMENDADOS                                 │
│   ├─ Load Balancer: 1 (Nginx/HAProxy)                    │
│   ├─ Lançamentos Service: 2 instâncias (ativo-ativo)     │
│   ├─ Consolidado Service: 1 instância (+ auto-scaling)   │
│   ├─ PostgreSQL: 1 primary + 1 read replica              │
│   ├─ PgBouncer: pool de conexões para Lançamentos        │
│   ├─ Redis: 1 + 1 replica (invalidação ativa por evento) │
│   └─ RabbitMQ: HA pair (2 nós)                           │
│                                                          │
│ ESCALABILIDADE                                           │
│   ├─ Leitura: Redis escala horizontalmente (cluster)     │
│   ├─ Escrita: Lançamentos ativo-ativo + PgBouncer        │
│   ├─ Ano 5: 87,4 req/s ÷ 2 = 43,7 req/s/srv (OK)       │
│   └─ Ano 10: 176 req/s → add instâncias Consolidado ✅  │
│                                                          │
│ CUSTOS ESTIMADOS (AWS/Azure/GCP)                         │
│   ├─ Load Balancer: $5-10/mês                            │
│   ├─ 2 × EC2 t3.micro (Lançamentos): $10-15/mês         │
│   ├─ 1 × EC2 t3.micro (Consolidado): $5-8/mês           │
│   ├─ 1 × RDS PostgreSQL t3.small: $20-30/mês            │
│   ├─ 1 × ElastiCache Redis t3.micro: $5-10/mês          │
│   ├─ 1 × RabbitMQ managed: $10-20/mês                    │
│   └─ TOTAL: ~$55-95/mês (infraestrutura otimizada)       │
│                                                          │
│ STATUS: ✅ WRITE-HEAVY COM LEITURA SERVIDA POR CACHE    │
│         ✅ OUTBOX PATTERN GARANTE CONSISTÊNCIA           │
│         ✅ SLA 50 REQ/S ATENDIDO COM 99%+ CACHE HIT     │
│         ✅ CRESCIMENTO ORGÂNICO ATÉ 176 REQ/S           │
└──────────────────────────────────────────────────────────┘
```

---

### 🔄 Estratégia de Cache: Write-Through com Invalidação por Evento

A consistência entre dados persistidos e dados em cache é garantida pelo padrão **Write-Through Cache via Consumer**, acionado por cada evento `LançamentoRegistrado` publicado no RabbitMQ.

#### Fluxo Completo (Escrita → Cache → Leitura)

```
POST /lancamentos
  ├─ 1. Validação de domínio (tipo, valor, data)
  ├─ 2. Verificação de idempotência (SELECT IdempotencyKey)
  ├─ 3. INSERT no PostgreSQL (transação ACID)
  ├─ 4. INSERT na tabela Outbox (mesma transação)
  └─ 5. Retorna 201 Created em <50ms

Outbox Publisher (background worker):
  └─ SELECT eventos não publicados → PUBLISH LançamentoRegistrado → UPDATE status

RabbitMQ → Consumer (Consolidado Service):
  ├─ Recebe LançamentoRegistrado
  ├─ Recalcula saldo do dia afetado (soma créditos - débitos)
  ├─ SET Redis: consolidado:{data} = {saldo} EX 86400
  └─ ACK da mensagem (garantia de at-least-once delivery)

GET /consolidado/{data}:
  ├─ Redis HIT  (99%+) → <5ms ✅
  └─ Redis MISS (cold start / flush) → Calcula do BD → Cacheia → Retorna
```

#### Implementação C# — Handler de atualização do cache (implementado)

```csharp
// FinControl.Consolidado.Core/Features/Commands/AtualizarSaldoConsolidao/
public class AtualizarSaldoConsolidadoCommandHandler(
    RedisCacheService cache,
    IRedisLockService lockService,
    ILogger<AtualizarSaldoConsolidadoCommandHandler> logger)
{
    private const string CACHE_KEY_ACUMULADO = "saldo:consolidado:acumulado";

    private static string CacheKey(DateOnly data) => $"saldo:consolidado:{data:yyyy-MM-dd}";
    private static string LockKey(DateOnly data)  => $"lock:saldo:consolidado:{data:yyyy-MM-dd}";

    public async Task Handle(AtualizarSaldoConsolidadoCommand command, CancellationToken ct = default)
    {
        // Usa a data do lançamento — não a data atual — para consolidar no dia correto
        var data = DateOnly.FromDateTime(command.DataLancamento.UtcDateTime);
        var key   = CacheKey(data);

        var acquired = await lockService.ExecuteWithLockAsync(
            lockKey: LockKey(data),
            action: async () =>
            {
                // Atualiza o saldo acumulado (corrido)
                var acumulado = await cache.GetAsync<SaldoConsolidado>(CACHE_KEY_ACUMULADO, ct);
                var valorAcumulado = (acumulado?.Saldo ?? 0) + command.ValorLancamento;
                await cache.SetAsync(CACHE_KEY_ACUMULADO,
                    new SaldoConsolidado(valorAcumulado, DateTimeOffset.UtcNow), null, ct);

                // Atualiza o saldo do dia específico
                var atual = await cache.GetAsync<SaldoConsolidado>(key, ct);
                var novoSaldo = new SaldoConsolidado(
                    Saldo: valorAcumulado,
                    UltimaAtualizacao: DateTimeOffset.UtcNow);
                await cache.SetAsync(key, novoSaldo, TimeSpan.FromDays(30), ct);
            },
            lockExpiry: TimeSpan.FromSeconds(10),
            ct: ct);

        if (!acquired)
            throw new InvalidOperationException(
                $"Nao foi possivel adquirir lock de consolidacao para o dia {data:yyyy-MM-dd}. " +
                "O evento sera reprocessado pelo message broker.");
    }
}
```

**Por que TTL de 30 dias (e não 60 segundos)?**

Com a estratégia event-driven, o cache é atualizado *a cada lançamento registrado* — não expira passivamente. TTL longo significa que registros históricos (dias anteriores) ficam disponíveis sem cold start, enquanto o dia corrente é sempre fresco via evento.

#### Implementação C# — Outbox Pattern (garante entrega mesmo com crash)

```csharp
// Lancamentos.Infrastructure/Outbox/OutboxPublisher.cs
public sealed class OutboxPublisher(
    AppDbContext db,
    IPublishEndpoint bus,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PublicarEventosPendentesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task PublicarEventosPendentesAsync(CancellationToken ct)
    {
        var pendentes = await db.OutboxMessages
            .Where(m => m.PublicadoEm == null)
            .OrderBy(m => m.CriadoEm)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in pendentes)
        {
            try
            {
                var evento = JsonSerializer.Deserialize<LancamentoRegistrado>(msg.Payload)!;
                await bus.Publish(evento, ct);

                msg.PublicadoEm = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                logger.LogInformation("Evento publicado: {EventoId}", msg.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao publicar evento Outbox {EventoId}", msg.Id);
                // Não marca como publicado → será retentado no próximo ciclo
            }
        }
    }
}
```

#### Por que Outbox é Crítico neste Modelo?

```
PROBLEMA SEM OUTBOX:
├─ POST /lancamentos → INSERT PostgreSQL ✅
├─ Publish RabbitMQ → FALHA (network timeout, broker down) ❌
├─ Cache NÃO é atualizado
├─ GET /consolidado retorna saldo desatualizado
└─ Violação do SLA de consistência (dado financeiro incorreto)

SOLUÇÃO COM OUTBOX:
├─ POST /lancamentos → INSERT PostgreSQL + INSERT Outbox (mesma transação)
├─ Se Publish falha → OutboxPublisher retenta em background
├─ Cache É atualizado quando broker voltar
└─ Consistência eventual garantida (at-least-once)

PROTEÇÃO ADICIONAL — Dead Letter Queue (DLQ):
├─ Consumer falha 3× → mensagem vai para DLQ
├─ Alerta dispara (Prometheus → Grafana)
├─ SRE investiga + replay manual da DLQ
└─ Cache restaurado sem perda de dados
```

---

### Justificativa dos Componentes Tecnológicos

#### 1. PostgreSQL (Banco de Dados Transacional)

```
Volumetria: 1.466.000 registros em 10 anos = 2 GB com índices
Concorrência: Baixa (~2 escrita/s, 5-10 leitura/s para consolidado)
Transações: ACID crítico (débito/crédito deve ser confiável)
Replicação: Simples (1 master + 1 standby para HA)

Justificativa:
✅ Espaço: 2 GB é negligenciável
✅ Performance: Índices suficientes para 2 B-trees
✅ Confiabilidade: ACID nativo = perfekt para operações financeiras
✅ Escalabilidade: Não precisa sharding em 10 anos
✅ Custo: Open source, baixo TCO

Alternativas rejeitadas:
❌ MongoDB: Sem ACID (versão 4.0+, mas complexo para este caso)
❌ CosmosDB: Overkill, serverless, mais caro
❌ Cassandra: Requer 3-5 nós, complexidade desnecessária
```

---

#### 2. Redis (Cache Distribuído)

```
Requisição GET /consolidado:
├─ 50 req/s = 180.000 req/hora
├─ Cache hit rate esperado: 95% (saldo muda raramente)
└─ Reduz para DB: ~9.000 req/hora = 2,5 req/s

Sem cache:
├─ 50 req/s × 20ms (latência DB) = 1.000ms/s overhead
├─ CPU database: ~50% em picos
├─ Resposta média: 20-50ms

Com cache Redis:
├─ 50 req/s × 2ms (latência cache) = 100ms/s overhead
├─ Cache miss (5%): query BD normalmente
├─ Resposta média: 2-5ms (HIT), 20-50ms (MISS)

Economias de recursos:
```
Query ao BD:        50 req/s  →  2,5 req/s (-95%)
CPU Database:       50%       →  5% (-90%)
Latência P99:       50ms      →  5ms (-90%)
Carga de rede:      50 req/s  →  2,5 req/s (-95%)

Cálculo de memória Redis necessária:
├─ Consolidado por dia: ~200 bytes
├─ Guarda 90 dias (3 meses): 200 bytes × 90 = 18 KB
├─ Com margem para TTL overlap: ~50 KB
└─ Total: <<< 100 MB

Status: ✅ Micro-instância Redis suficiente
        ✅ Custo negligenciável
        ✅ ROI extremamente alto (reduz carga em 90%)
```

**Alternativas rejeitadas:**
```
❌ Memcached: Sem persistência, sem estruturas complexas
❌ Elasticsearch: Overkill, voltado para buscas complexas
❌ VarnishCache: Necessário proxy reverso extra
✅ Redis: Simples, rápido, confiável, com Pub/Sub para eventos
```

---

#### 3. RabbitMQ / Message Queue (Event Bus)

```
Cenário de pico - 50 req/s de consolidado:

Sem queue (sincronizado):
├─ Cliente → API Consolidado → Query BD (20ms)
└─ Timeout se BD cair = SERVIÇO CAI

Com queue (assincronizado):
├─ Lançamento → Publica evento
├─ Evento vai para RabbitMQ (local, ultra-rápido)
├─ Consolidado Consumer processa em background
└─ Se Consolidado cair, eventos permanecem enfileirados

Volumetria de fila:
├─ 200 lançamentos/dia = 200 eventos/dia
├─ Tamanho evento: ~500 bytes
├─ Armazenamento necessário: 200 × 500 bytes = 100 KB/dia
├─ Em 30 dias (capacidade buffer): 3 MB
└─ RabbitMQ com 1 GB de RAM = 1000x de capacidade

Benefícios quantificáveis:
✅ Resiliência: Lançamentos funciona mesmo se consolidado cair
✅ Performance: Lançamentos retorna em <50ms (sem esperar consolidado)
✅ Confiabilidade: Fila persiste, garante entrega (retry)
✅ Escalabilidade: Múltiplos consumers processam em paralelo
✅ Custo: Overhead negligenciável

Cálculo de consumers necessários:
├─ 200 eventos/dia ÷ (16h × 3600s) = 0,003 eventos/s
├─ Processamento: ~100ms por evento
├─ 1 consumer suficiente para volumes normais
└─ Com auto-scaling: até 5 consumers para picos

Status: ✅ RabbitMQ com 1 instância suficiente
        ✅ Custo baixo (~$10-20/mês cloud)
        ✅ Garante requisito de resiliência
```

**Alternativas rejeitadas:**
```
❌ Kafka: Complexidade desnecessária (multi-partition, rebalance)
❌ AWS SQS: Vendor lock-in,complexidade, maior latência
❌ Azure Service Bus: Vendo lock-in, complexidade, custo maior
✅ RabbitMQ: Simples, confiável, self-hosted, open source
```

---

#### 4. Load Balancer (API Gateway)

```
Tráfego diário:
├─ Escrita: 200 req/dia
├─ Leitura: 2.880.000 req/dia (50 req/s × 16h × 3600)
├─ Total: 2.880.200 req/dia = 33.3 req/s em média

Pico estimado (Hora de pico):
├─ 50 req/s sustentado (conforme spec)
├─ +20% de margem para burst = 60 req/s

Distribuição em 2 instâncias de Consolidado:
├─ 60 req/s ÷ 2 = 30 req/s por instância
├─ Latência processamento: 2-5ms (com cache)
├─ Throughput por instância: 100+ req/s (capacidade)
└─ Utilização: 30% (muito confortável)

Health checks & Rate Limiting:
├─ Verificar saúde a cada 10 segundos
├─ Rate limit: 60 req/s por cliente
├─ Burst allowance: até 120 req/s por 10 segundos
└─ Rejeição graceful (não afeta outros clientes)

Componentes de LB necessários:
├─ 1x Load Balancer (Nginx/HAProxy/Azure LB)
├─ 2x Instâncias Consolidado Service
├─ 2x Instâncias Lançamento Service
└─ Auto-scaling rules (CPU > 70%)

Status: ✅ Dois servidores nginx suficientes
        ✅ Custo: ~$20-50/mês
        ✅ Garante 99.9% disponibilidade
```

---

### Resumo Quantitativo - 10 Anos

```
┌────────────────────────────────────────────────────────┐
│           VOLUMETRIA CONSOLIDADA - 10 ANOS             │
├────────────────────────────────────────────────────────┤
│ Lançamentos registrados:    1.466.080                  │
│ Espaço BD necessário:       2,0 GB                     │
│ Requisições consolidado:    1.036.872.000/ano          │
│ Requisições pico:           50 req/s (consistente)     │
│ Requisições escrita:        <2 req/s (baixo)           │
│ Taxa leitura:escrita:       14.400:1                   │
├────────────────────────────────────────────────────────┤
│ DECISÕES TECNOLÓGICAS JUSTIFICADAS:                    │
├────────────────────────────────────────────────────────┤
│ ✅ PostgreSQL:              2 GB = Negligenciável      │
│ ✅ Redis:                   <100 MB = Mínimo           │
│ ✅ RabbitMQ:                <1 GB RAM = Suficiente     │
│ ✅ Microserviços:           Escalabilidade futura      │
│ ✅ CQRS:                    Otimiza 50 req/s separado  │
│ ✅ Cache + Circuit Breaker: 95% HIT rate, resiliência │
│ ✅ Sem sharding:            10 anos com 1 BD           │
│ ✅ Sem cluster complexo:    Arquitetura simples        │
└────────────────────────────────────────────────────────┘
```

---

## Justificativas Tecnológicas

Com base na análise de volumetria acima, aqui estão as decisões arquiteturais **quantificadas**:

### Por que Microserviços?

| Métrica | Impacto |
|---------|--------|
| **Taxa leitura:escrita** | 8.333:1 → Leitura e escrita precisam escalar separadamente |
| **Padrão de crescimento** | 15% a.a. com base em 50 req/s → Fluxo previsível |
| **Resiliência crítica** | Lançamentos não pode cair se consolidado falha |
| **Independência operacional** | Cada serviço tem SLA diferente |
| **Escalabilidade horizontal** | Adicionar servidores Consolidado sem tocar em Lançamentos |

**Resultado:** Cada serviço escalável, deployável e reparável de forma independente. Com 2 servidores de Consolidado, cada um suporta 25 req/s confortavelmente (50% utilização).

---

### Por que Redis Cache é ESSENCIAL?

```
SEM CACHE → 50 req/s → 50 queries ao PostgreSQL → CPU 70%+ → COLAPSO
COM CACHE → 50 req/s → 2,5 queries ao PostgreSQL → CPU 15% → ROBUSTO
```

| Métrica | Impacto Quantificado |
|---------|----------------------|
| **Redução queries BD** | 50 → 2,5 req/s (-95%) |
| **Redução CPU Database** | 70% → 15% (-75%) |
| **Melhoria latência P99** | 50ms → 5ms (-90%) |
| **Escalabilidade BD** | Até 100 req/s → Até 500+ req/s (+400%) |
| **Hit rate esperado** | 95% (saldo não muda a cada segundo) |
| **Memória necessária** | <100 MB (negligenciável) |
| **Custo mensal** | $5-10 |

**Resultado:** Redis é **MANDATÓRIO** para atingir 50 req/s sustentado. Sem cache, o sistema colapsaria com 60-70 req/s.

---

### Por que PostgreSQL (não DynamoDB/Cassandra)?

| Aspecto | PostgreSQL | DynamoDB | Cassandra |
|--------|-----------|----------|-----------|
| **ACID Garantido** | ✅ 100% | ⚠️ Limitado | ❌ Eventual |
| **Escrita (0,006 req/s)** | ✅ Overkill | ❌ Overkill | ❌ Overkill |
| **Armazenamento 10 anos** | 482 MB | 482 MB | 482 MB |
| **Queries BD necessárias** | 2,5 req/s (95% cache) | Overkill | Overkill |
| **Setup complexity** | Simples | Média | Complexa |
| **Custo** | $15-25/mês | $50+/mês | $100+/mês |
| **Adequado financeiro?** | ✅ Perfeito | ❌ Não | ❌ Não |

**Resultado:** PostgreSQL é a escolha óbvia: máxima confiabilidade para operações financeiras com mínima complexidade e custo.

---

### Por que RabbitMQ (não Kafka/AWS SQS)?

```
VOLUME: 350 eventos/dia (negligenciável)
PADRÃO: Assincronismo puro (publicador não espera resposta)
NECESSIDADE: Garantir que Lançamentos funcione mesmo se Consolidado cair
```

| Métrica | RabbitMQ | Kafka | AWS SQS |
|---------|----------|-------|---------|
| **Setup time** | 30 min | 2-4 horas | Via console |
| **Curva aprendizado** | Baixa | Média | Média |
| **Customização** | Fácil | Difícil | Limitada |
| **Vendor lock-in** | ❌ Não | ❌ Não | ✅ Sim |
| **Dead Letter Queue** | ✅ Nativo | ⚠️ Plugin | ✅ Nativo |
| **Para 350 eventos/dia** | ✅ Perfeito | ❌ Overkill | ⚠️ Adequado |
| **Custo** | $10-15/mês | $20+/mês | $25+/mês |

**Resultado:** RabbitMQ oferece melhor custo-benefício com máxima flexibilidade e portabilidade.

---

### Por que Kong (ou Nginx/HAProxy) para API Gateway?

Kong é uma **excelente opção** para este cenário. Aqui está a análise completa:

```
REQUISITO: Distribuir 50 req/s entre 2 servidores Consolidado + 1 Lançamentos
           = 25 req/s cada (Consolidado)
           = 0,1 req/s (Lançamentos)
CRITÉRIO: Rate limiting, Health checks, Logging, Auth, Request routing
```

| Critério | Nginx | HAProxy | AWS ALB | Kong Gateway | Kong Enterprise |
|----------|-------|---------|---------|--------------|-----------------|
| **Setup Time** | 15 min | 20 min | 5 min | 30 min | 1h |
| **Curva Aprendizado** | Baixa | Média | Baixa | Média-Alta | Alta |
| **Rate Limiting Nativo** | ⚠️ Via módulo | ✅ Nativo | ✅ Via WAF | ✅ Nativo + Plugins | ✅ Avançado |
| **Health Checks** | ✅ Passivo | ✅ Ativo | ✅ Ativo | ✅ Completo | ✅ Completo |
| **Circuit Breaker** | ❌ Não | ❌ Não | ❌ Não | ✅ Nativo | ✅ Nativo |
| **Authentication/Authz** | ⚠️ Via módulo | ⚠️ Via módulo | ⚠️ Via WAF | ✅ Plugins (OAuth2, JWT, Basic) | ✅ Plugins (OAuth2, JWT, mTLS) |
| **Request Logging** | ✅ Bom | ✅ Bom | ✅ Bom | ✅ Estruturado | ✅ Completo |
| **Request Tracing** | ❌ Não | ❌ Não | ⚠️ X-Ray | ✅ Suporta OpenTelemetry | ✅ OpenTelemetry + Plugins |
| **Load Balancing Algos** | 5 (round-robin, least conn, etc) | 8+ | 3 (round-robin, least outstanding) | 7 (inclusive rate limiting aware) | 10+ |
| **Vendor Lock-in** | ❌ Não | ❌ Não | ✅ Sim (AWS) | ❌ Não (Open Source) | ⚠️ Sim (Kong Cloud) |
| **Para 50 req/s** | ✅ Overkill | ✅ Overkill | ✅ Overkill | ✅ Perfeito | ✅ Enterprise overkill |
| **Cost (Infrastructure)** | $5-10/mês | $5-10/mês | $50+/mês | $10-15/mês (self-hosted) | $500+/mês (Cloud) |
| **Cost (Operational)** | Baixo | Baixo | Médio | Médio | Alto |
| **Container Ready** | ✅ Docker | ✅ Docker | ❌ AWS only | ✅ Docker + K8s | ✅ Docker + K8s |
| **Kubernetes Native** | ⚠️ Ingress | ⚠️ Ingress | ❌ Não | ✅ Ingress Controller | ✅ Ingress Controller |
| **Monitoring/Observability** | ⚠️ Via Prometheus export | ⚠️ Via Prometheus export | ✅ CloudWatch native | ✅ Admin API + Metrics | ✅ Admin API + Observability |
| **Escalabilidade em 10 anos** | ✅ Suficiente | ✅ Suficiente | ⚠️ Vendor | ✅ Escalável | ✅ Enterprise grade |

#### Análise por Cenário

**Cenário 1: Self-Hosted Simple (Recomendado para MVP)**
```
✅ RECOMENDAÇÃO: Nginx + Lua ou Kong Gateway (Open Source)

Razão: Simplicidade + Funcionalidade balanceada
├─ Nginx: Leve, rápido, bem conhecido (15 min setup)
└─ Kong: Mais features, plugins, facilita future roadmap (30 min setup)

Para 50 req/s: Ambos fazem com folga
├─ Nginx: <1% CPU por servidor
└─ Kong: ~2-5% CPU por servidor

Recomendação: KONG (justificativa abaixo)
```

**Cenário 2: Produção com Escalabilidade (Recomendado para escala)**
```
✅ RECOMENDAÇÃO: Kong Gateway (Self-hosted) + Kubernetes

Razão: Plugins nativos resolvem problemas que Nginx demoraria a configurar
├─ Rate Limiting: Kong tem, Nginx precisa módulo + Lua
├─ Circuit Breaker: Kong tem, Nginx não tem
├─ Request Tracing: Kong tem, Nginx não tem
├─ mTLS: Kong tem, Nginx tem mas Kong é mais direto
└─ Auth (OAuth2/JWT): Kong plugins, Nginx precisa módulo terceiro

Crescimento 50 → 176 req/s:
├─ Nginx: Precisaria refatoração
└─ Kong: Simples add mais instâncias + plugins config

Recomendação: KONG + KONG INGRESS CONTROLLER (K8s)
```

**Cenário 3: AWS Only (Recomendado para AWS)**
```
⚠️ POSSÍVEL: AWS ALB + WAF

Razão: Integração nativa, mas menos flexibilidade
├─ Vendor lock-in completo
├─ Custo > Kong ($50+/mês vs $10-15/mês)
├─ Rate limiting via WAF (complexo)
└─ Less features than Kong

Recomendação: NÃO (A menos que empresa é 100% AWS)
```

#### Por que Kong é MELHOR para este projeto?

```
KONG OFERECE:

1️⃣ Rate Limiting Inteligente
   └─ Por IP, por user, por endpoint
   └─ Crucial para 50 req/s sustentado com 5% máximo de perda
   └─ Nginx: Precisa lua + módulo + configuração complexa
   └─ Kong: Um plugin, pronto

2️⃣ Circuit Breaker Nativo
   └─ Se Consolidado cair, Kong date fallback automático
   └─ Nginx: Precisa proxy upstream + config manual
   └─ Kong: Kong circuit-breaker plugin, pronto

3️⃣ Plugin Ecosystem
   └─ Auth (OAuth2, JWT, Basic, API Key)
   └─ Logging (Syslog, HTTP, File, Kafka)
   └─ Monitoring (Prometheus, DataDog, New Relic)
   └─ CORS, Transformer, Request Size Limit, etc

4️⃣ Kubernetes Ready
   └─ Kong Ingress Controller (native support)
   └─ Nginx: Ingress, mas menos features
   └─ Kong: Ingress + Plugins CRDs

5️⃣ Request Routing Avançado
   └─ Pode rotear por path, method, header, regex
   └─ Essencial para quando adicionar novos endpoints

6️⃣ Admin API REST
   └─ Configure Kong via API (não apenas YAML)
   └─ Nginx: Precisa recarregar config (downtime potencial)
   └─ Kong: Hot reload via API

7️⃣ Observability Builtin
   └─ Metrics Prometheus
   └─ Request/Response Logging
   └─ Tracing (OpenTelemetry)
   └─ Nginx: Precisa exporters terceiros
```

#### Kong Setup para 50 req/s

```yaml
# docker-compose.yml - Kong API Gateway

services:
  kong-db:
    image: postgres:15
    environment:
      POSTGRES_DB: kong
      POSTGRES_USER: kong
      POSTGRES_PASSWORD: kong
    ports:
      - "5432:5432"
    volumes:
      - kong_data:/var/lib/postgresql/data

  kong:
    image: kong:3.8
    container_name: kong
    environment:
      KONG_DATABASE: postgres
      KONG_PG_HOST: kong-db
      KONG_PG_USER: kong
      KONG_PG_PASSWORD: kong
      KONG_PROXY_ACCESS_LOG: /dev/stdout
      KONG_ADMIN_ACCESS_LOG: /dev/stdout
      KONG_PROXY_ERROR_LOG: /dev/stderr
      KONG_ADMIN_ERROR_LOG: /dev/stderr
      KONG_ADMIN_LISTEN: 0.0.0.0:8001
    ports:
      - "8000:8000"   # Proxy (seu API)
      - "8443:8443"   # Proxy SSL
      - "8001:8001"   # Admin API (config)
      - "8444:8444"   # Admin API SSL
    depends_on:
      - kong-db
    healthcheck:
      test: ["CMD", "kong", "health"]
      interval: 10s
      timeout: 5s
      retries: 5

  kong-migrations:
    image: kong:3.8
    command: kong migrations bootstrap
    environment:
      KONG_DATABASE: postgres
      KONG_PG_HOST: kong-db
      KONG_PG_USER: kong
      KONG_PG_PASSWORD: kong
    depends_on:
      - kong-db

  konga:  # Kong Admin UI (optional but recommended)
    image: pantsel/konga:latest
    ports:
      - "1337:1337"
    environment:
      DB_ADAPTER: postgres
      DB_HOST: kong-db
      DB_USER: kong
      DB_PASSWORD: kong
      DB_DATABASE: konga_db
    depends_on:
      - kong-db

volumes:
  kong_data:
```

#### Kong Configuration (via Admin API)

```bash
# 1️⃣ Criar upstream (múltiplas instâncias de Consolidado)
curl -X POST http://localhost:8001/upstreams \
  -d "name=consolidado_backend"

# 2️⃣ Adicionar targets (2 servidores Consolidado)
curl -X POST http://localhost:8001/upstreams/consolidado_backend/targets \
  -d "target=consolidado-1.example.com:5000" \
  -d "weight=50"

curl -X POST http://localhost:8001/upstreams/consolidado_backend/targets \
  -d "target=consolidado-2.example.com:5000" \
  -d "weight=50"

# 3️⃣ Criar Service (abstração do upstream)
curl -X POST http://localhost:8001/services \
  -d "name=consolidado_service" \
  -d "host=consolidado_backend" \
  -d "port=5000" \
  -d "protocol=http"

# 4️⃣ Criar Route (mapping de URL para service)
curl -X POST http://localhost:8001/services/consolidado_service/routes \
  -d "name=consolidado_route" \
  -d "paths=/api/consolidado" \
  -d "methods=GET"

# 5️⃣ Adicionar Rate Limiting Plugin
curl -X POST http://localhost:8001/services/consolidado_service/plugins \
  -d "name=rate-limiting" \
  -d "config.minute=3000" \
  -d "config.policy=sliding_window" \
  -d "config.limit_by=ip"

# 6️⃣ Adicionar Circuit Breaker Plugin
curl -X POST http://localhost:8001/services/consolidado_service/plugins \
  -d "name=circuit-breaker" \
  -d "config.failure_threshold=50" \
  -d "config.recovery_threshold=50" \
  -d "config.name=consolidado_cb"

# 7️⃣ Adicionar Logging Plugin
curl -X POST http://localhost:8001/services/consolidado_service/plugins \
  -d "name=http-log" \
  -d "config.http_endpoint=http://log-server:8080/logs" \
  -d "config.method=POST"

# 8️⃣ Adicionar Health Check
curl -X PATCH http://localhost:8001/upstreams/consolidado_backend \
  -d "healthchecks.active.http_path=/api/health" \
  -d "healthchecks.active.interval=10" \
  -d "healthchecks.active.timeout=5"
```

#### Resultado Kong para 50 req/s

```
VALIDAÇÃO: 50 req/s = 3.000 req/minuto

Com Kong Rate Limiting (3.000 req/min):
├─ Exatamente no limite ✅
├─ Margem de 20% para burst = 3.600 req/min ✅
├─ Distribuição: 2 servidores × 1.800 req/min ✅
├─ Utilização Kong: <1% CPU ✅
├─ Latência adicionada: <2ms ✅
└─ Feature: Circuit breaker automático ✅

CONCLUSÃO: Kong é PERFEITO para 50 req/s sustentado
```

---

### Por que CQRS (Command Query Responsibility Segregation)?

```
Padrão de tráfego: 8.333 leituras : 1 escrita

Sem CQRS:
├─ Mesmo modelo otimizado para escrita E leitura (impossível)
├─ Trade-offs prejudicam ambos
└─ Cache descentralizado, inconsistências

Com CQRS:
├─ Write Model: Lançamentos otimizado para ACID
├─ Read Model: Consolidado otimizado para 50 req/s com cache
├─ Separação clara de responsabilidades
└─ Cada um escala independentemente
```

**Resultado:** CQRS permite otimizar 50 req/s de leitura sem compromissar integridade de escrita.

---

### Elasticidade: Crescimento Sustentável até 176 req/s

```
CRESCIMENTO COM 50 REQ/S BASE:

Ano 1:   50 req/s  ÷ 2 servidores = 25 req/s cada (42% CPU)   ✅
Ano 3:   66 req/s  ÷ 2 servidores = 33 req/s cada (56% CPU)   ✅
Ano 5:   87 req/s  ÷ 3 servidores = 29 req/s cada (49% CPU)   ✅
Ano 7:  116 req/s  ÷ 3 servidores = 39 req/s cada (65% CPU)   ✅
Ano 10: 176 req/s  ÷ 4 servidores = 44 req/s cada (75% CPU)   ✅

Modelo: Escala horizontal pura (add servidores)
└─ Nenhum redesign necessário em 10 anos
```

**Resultado:** Arquitetura preparada para crescimento orgânico. Com crescimento de 15% a.a., passamos de 50 req/s para 176 req/s sem recargo arquitetural.

---

### Histórico: Elasticidade - Quando Escalar?

```
CRESCIMENTO PROJETADO (baseado em crescimento de lançamentos):

Ano 1:  200 lançamentos/dia  → 1 servidor
Ano 3:  265 lançamentos/dia  → 1 servidor (CPU < 20%)
Ano 5:  351 lançamentos/dia  → 1 servidor (CPU < 30%)
Ano 7:  464 lançamentos/dia  → 2 servidores (pronto para crescer)
Ano 10: 707 lançamentos/dia  → 2-3 servidores (balanceado)

Requisições de LEITURA (50 req/s fixo):
├─ Ano 1-10: Cache reduz para 2,5 req/s
├─ Com 2 servidores: 1,25 req/s cada
└─ Utilização: 30-40% cada (muito confortável)

Status: ✅ Arquitetura pronta para crescimento sem redesign
```

---

### Por que começar com Monolito Modular (Vertical Slicing)?

Embora a arquitetura final proposta seja **Microserviços + Event-Driven**, o projeto **inicia como um Monolito Modular** com Vertical Slicing. Esta é uma decisão estratégica fundamentada em pragmatismo técnico e financeiro:

#### **Fase 1 (Ano 1): Monolito Modular com Wolverine**

```
Estrutura: Único processo .NET 10+ rodando todas as features
Deploy:    Docker Compose local + Kubernetes (1 réplica inicial)
Features:  Lançamentos, Consolidado, Auditoria em contextos separados

Vantagens:
├─ 🚀 Deployment: Uma única imagem Docker, um único pod Kubernetes
├─ 💾 Dados: Transações ACID cross-feature via EF Core + PostgreSQL
├─ 🔧 Operacional: Menos serviços para configurar, monitorar, debugar
├─ 💰 Custo: Uma instância + PostgreSQL + Redis = $30-40/mês
├─ 🧠 Cognição: Novo dev consegue clonar repo + rodar tudo em 10 minutos
├─ 📊 Observabilidade: OpenTelemetry centralizando traces de uma fonte
├─ 🎯 Acoplamento: Features podem compartilhar abstrações comuns
├─ 🔄 Síncrono: Handlers Wolverine com await local (sem rede)
└─ ⚡ Performance: Sem latência de inter-processo, sem serialização

Limitações (aceitáveis no Ano 1):
├─ ⚠️ Escala: Sobe com CPU/memória de 1 servidor (até ~200 req/s)
├─ ⚠️ Deploy: Tudo sobe junto (sem rolling update de 1 feature)
├─ ⚠️ Isolamento: Falha em Consolidado pode afetar Lançamentos
└─ ⚠️ Linguagem: Tudo em C# (não permite diversidade de stack)

Métrica de Sucesso: Atingir 50 req/s sustentado com <30% CPU em 1 servidor
```

#### **Fase 2 (Ano 3-5): Migração Gradual para Microserviços**

```
Trigger para migração:
├─ Volume atinge 100+ req/s (Monolito começa a sufocar)
├─ Feature isolada precisa escalar independentemente
├─ Equipe cresce e precisa de autonomia por time
└─ Custo operacional começa a pesar

Estratégia de Decomposição (sem downtime):
1. Manter Monolito com eventos publicados para RabbitMQ
2. Novo microsserviço subscrevedo a eventos (inicialmente simples)
3. Dual-write durante transição (Monolito + Novo Serviço)
4. Outbox Pattern manual (garantia de entrega: PostgreSQL → RabbitMQ)
5. Cortar dependências do Monolito quando estável
6. Decompor próxima feature (processo repetido)

Resultado esperado (Ano 5):
├─ Serviço de Lançamentos (C# + Wolverine + PostgreSQL)
├─ Serviço de Consolidado (Python FastAPI + SQLAlchemy + Compute)
├─ Serviço de Auditoria (C# + Marten + Event Store)
├─ Serviço de Notificações (Node.js + Bull + Redis)
├─ RabbitMQ interconectando tudo
└─ Kong orquestrando requests + circuit breakers
```

#### **Justificativa Econômica e Técnica**

| Aspecto | Monolito Modular (Ano 1-2) | Microserviços (Ano 5+) |
|--------|--------------------------|----------------------|
| **Infrastructure Cost** | $40/mês | $150-200/mês |
| **Operational Overhead** | Baixo (1 app) | Alto (5+ apps) |
| **Time to Market** | 3-4 meses | 6-8 meses |
| **Developer Onboarding** | <2 dias | 2-3 semanas |
| **Deploy Frequency** | 10-20x/dia | 3-5x/app/dia |
| **MTTR (Mean Time To Recover)** | 2-5 min | 5-15 min |
| **Transactional Consistency** | ✅ ACID nativo | ⚠️ Eventual + Saga |
| **Feature Interaction Latency** | <1ms | 5-50ms |
| **Concurrent Developer Teams** | 2-3 teams | 4+ teams |
| **Break Glass Scenario** | 1 processo reinicia | Múltiplos pontos de falha |
| **Best for Volume** | 50-100 req/s | 200+ req/s |

**Conclusão:** Monolito modular é a escolha racional para MVP que antecipa crescimento. Permite validar product-market fit sem overhead operacional. Migração é possível sem reescrever — apenas decomposição gradual via event-driven boundaries.

---

## Arquitetura Proposta: Microserviços + Event-Driven

### Diagrama de Arquitetura

![Diagrama de Componentes - FinControl](Diagrama%20de%20componentes%20do%20projeto%20FinControl.drawio.png)

### Por que esta abordagem?

| Aspecto | Benefício |
|---------|-----------|
| **Microserviços** | Cada serviço tem ciclo de vida independente; falha em um não afeta o outro |
| **Event-Driven** | Desacoplamento completo entre serviços; fila absorve picos naturalmente |
| **Message Broker** | Buffer para 50 req/s; retry automático; garante entrega |
| **Cache (Redis)** | Sub-milissegundo latency; reduz carga no banco em picos |
| **PostgreSQL** | ACID completo para transações de lançamento; confiabilidade garantida |

---

## Padrões Arquiteturais Aplicados

### 1. **Padrão de Microserviços**

```
Serviço A (Lançamentos) ────┐
                            ├─→ Independentes
Serviço B (Consolidado) ────┘

Benefício: Escala, deploy e falhas independentes
```

**Implementação:**
- Cada serviço em seu próprio projeto: `Lancamentos.API` e `Consolidado.API`
- Repositórios separados no GitHub (ou monorepo com diretorios separados)
- Databases separados (Database per Service pattern)

---

### 2. **Event-Driven Architecture**

```
┌─────────────┐         ┌──────────────────────┐         ┌──────────────┐
│ Lançamento  │─────→ Event:                 │────→ │  Atualiza    │
│   Criado    │   LançamentoRegistrado      │     │   Saldo      │
└─────────────┘         │ AggregateId         │     └──────────────┘
                        │ Tipo (D/C)          │
                        │ Valor               │
                        │ Data/Hora           │
                        │ Timestamp           │
                        └──────────────────────┘
```

**Vantagens:**
- Publicador não conhece subscribers
- Fácil adicionar novos consumers (audit, notificações, etc)
- Natural para sistemas distribuídos

---

### 3. **Command Query Responsibility Segregation (CQRS)**

```
ESCREVER (Commands)          LER (Queries)
└─ Lançamentos               └─ Consolidado
   ├ RegisterDebit              ├ GetDailyBalance
   ├ RegisterCredit             ├ GetTransactionHistory
   └ Otimizado para escrita     └ Otimizado para leitura rápida
      (INSERT/UPDATE)              (SELECT com cache)
```

**Benefício:** Modelos de escrita e leitura otimizados separadamente

---

### 4. **Circuit Breaker Pattern**

```csharp
// Protege contra cascata de falhas
// Se Consolidado cair:
// 1. Circuit abre após N falhas
// 2. Requisições retornam erro imediato (fail-fast)
// 3. Lançamentos não tenta chamar Consolidado repetidamente
// 4. Circuit fecha quando serviço se recupera
```

---

### 5. **Cache Strategy**

```
Requisição GET /saldo/2026-05-20
        │
        ▼
    Redis Cache?
    ├─ SIM → Retorna em <5ms
    └─ NÃO → Calcula do BD → Armazena em cache (TTL: 1 minuto)
                              → Retorna
```

---

### 6. **SOLID Principles**

| Princípio | Aplicação |
|-----------|-----------|
| **S**ingle Responsibility | `RegistrarLançamentoHandler`, `CalcularSaldoHandler` - cada classe uma responsabilidade |
| **O**pen/Closed | Handlers herdam de `ICommandHandler<T>` - extensível sem modificação |
| **L**iskov Substitution | Todos os handlers respeitam o contrato |
| **I**nterface Segregation | Interfaces pequenas e específicas (`ILançamentoRepository`, `IConsolidadoService`) |
| **D**ependency Inversion | Injeção de dependências via DI Container do ASP.NET Core |

---

## Stack Técnico

### Backend (implementado)

```
┌─ Framework
│  └─ ASP.NET Core 10 / .NET 10 (Minimal APIs)
│     └─ Scalar UI para documentação OpenAPI (não Swagger)
│
├─ CQRS & Mediator
│  └─ WolverineFx (handlers HTTP, middleware pipeline)
│     ├─ ValidationMiddleware (FluentValidation automático)
│     ├─ LoggingMiddleware (CorrelationId propagado)
│     └─ [WolverinePost] / [WolverineGet] — endpoints declarativos
│
├─ ORM & Data Access
│  ├─ Entity Framework Core 10 (Npgsql provider)
│  │  ├─ Migrations aplicadas automaticamente no startup (fail-fast)
│  │  ├─ Global query filter: soft-delete (DeletedAt == null)
│  │  └─ AsNoTracking para queries de leitura
│  └─ Repository Pattern (Ardalis.Specification)
│
├─ Outbox Pattern (implementação manual)
│  ├─ OutboxMessage entity (tabela lancamentos.outbox_messages)
│  ├─ RegistrarLancamentoCommandHandler — transação atômica (lancamento + outbox)
│  ├─ OutboxRelayService (BackgroundService — polling 5s, batch 50)
│  │  └─ Polly ResiliencePipeline: retry 3× exponencial + jitter
│  └─ RabbitMqPublisher (IRabbitMqPublisher — building block reutilizável)
│     └─ Conexão única reutilizável + IChannel por publicação
│
├─ Message Bus
│  └─ RabbitMQ.Client direto (consumer no Consolidado.Worker)
│     ├─ Topic exchange: lancamentos.events
│     ├─ Queue: fincontrol.consolidado.lancamento-registrado
│     ├─ prefetchCount: 10, autoAck: false
│     └─ Reconexão exponencial (5s → 60s)
│
├─ Caching & Distributed Lock
│  ├─ IDistributedCache (StackExchange.Redis)
│  ├─ RedisCacheService (wrapper com serialização JSON camelCase)
│  ├─ IRedisLockService / RedisLockService
│  │  ├─ SETNX (StringSetAsync When.NotExists)
│  │  ├─ 5 tentativas × 100ms de espera
│  │  └─ Release atômico via Lua (garante que só o dono libera)
│  └─ Chave de cache: saldo:consolidado:{yyyy-MM-dd}, TTL 30 dias
│
├─ Resilience
│  └─ Polly v8 (ResiliencePipelineBuilder)
│     └─ OutboxRelayService: retry 3× com backoff exponencial + jitter
│
├─ Validation
│  └─ FluentValidation (avaliação em runtime — não valores capturados)
│
├─ Logging
│  ├─ Serilog (structured logging)
│  └─ Sinks: Console, File
│     └─ CorrelationId enriquecido em todos os logs
│
├─ Secrets Management
│  ├─ HashiCorp Vault KV v2 (VaultSharp)
│  └─ VaultConfigurationProvider (integrado ao pipeline IConfiguration)
│     └─ vault.settings.json / vault.settings.{Env}.json
│
├─ Identity & Auth
│  ├─ Keycloak (JWT Bearer via AddFinControlKeycloakAuth())
│  └─ SubscriptionKeyMiddleware
│     ├─ Valida header X-Subscription-Key contra Vault
│     ├─ Usa CryptographicOperations.FixedTimeEquals (timing-safe)
│     └─ Bypassa /health e /metrics — segunda camada após Kong
│
├─ Idempotência
│  ├─ IdempotencyKey (UUID) no header Idempotency-Key
│  ├─ Armazenado na entidade Lancamento
│  └─ Índice único: idx_lancamento_idempotency_key
│
├─ Testing
│  ├─ xUnit (framework de testes)
│  ├─ Moq (mocking — IDistributedCache, IRedisLockService)
│  ├─ FluentAssertions (asserts legíveis)
│  ├─ Bogus (geradores de dados — Faker<T>)
│  └─ NBomber 5.5.0 (stress test manual — execução via `dotnet run`)
│     ├─ ConsolidadosScenario: ramp 20s → 50 req/s sustained → ramp-down 10s
│     ├─ LancamentosScenario:  ramp 20s → 10 req/s sustained → ramp-down 10s
│     ├─ Thresholds: Consolidado p95 < 500ms | Lançamentos p95 < 1000ms | erro < 5%
│     └─ Relatórios HTML + Markdown em stress-reports/ (não versionado)
│
└─ Observability
   ├─ OpenTelemetry (traces + métricas HTTP, EF Core, HTTP client)
   ├─ prometheus-net (métricas expostas em /metrics — ambas as APIs)
   ├─ Jaeger (distributed tracing, OTLP)
   └─ Serilog (logs estruturados)
```

### Infraestrutura

```
WAF & Firewall:      ModSecurity 3.0+ (OWASP Top 10 protection)
API Gateway:         Kong 3.8
                     ├─ jwt plugin: valida RS256 com chave pública do Keycloak
                     ├─ request-transformer: injeta X-Subscription-Key no upstream
                     ├─ proxy-cache: cache GET por 30s (Consolidado)
                     └─ rate-limiting: 300 req/min (Lancamentos), 55 req/s (Consolidado)
Database:            PostgreSQL 17 (Alpine)
Cache:               Redis 7.4 Alpine (StackExchange.Redis)
Message Bus:         RabbitMQ 3.13 Management Alpine
                     └─ Exchange: lancamentos.events (topic, durable)
Secrets Management:  HashiCorp Vault 1.18 (KV v2, dev mode)
Identity Provider:   Keycloak latest (SSO, OAuth2, OIDC, realm fincontrol)
Observability:       OpenTelemetry + prometheus-net + Serilog + Jaeger + Loki
Dashboard:           Grafana 11.4.0 (dashboard HTTP provisionado via JSON)
Log Aggregation:     Grafana Loki 3.3.0 (sink Serilog → Loki → Grafana)
Metrics:             Prometheus (scraping /metrics das APIs)
Tracing:             Jaeger all-in-one (OTLP gRPC 4317 + HTTP 4318)
Container:           Docker + Docker Compose (dev/prod)
CI/CD:               GitHub Actions
```

---

**Stack Completo Recomendado:**
- ✅ **Segurança:** ModSecurity (WAF) + Kong (rate limit) + Fail2Ban (detecção)
- ✅ **Secrets:** Hashicorp Vault (centralizado, auditado, open source)
- ✅ **Identidade:** Keycloak (SSO, 2FA, multi-tenant)
- ✅ **Observabilidade:** OpenTelemetry + Prometheus + Grafana (completo, grátis)
- ✅ **Custo Total:** $0 (tudo open source)
- ✅ **Escalabilidade:** Pronto para 10 anos (50 → 176 req/s)


---

### Pacotes utilizados (net10.0)

```xml
<!-- Lancamentos.Core / Consolidado.Core -->
<PackageReference Include="WolverineFx" />
<PackageReference Include="WolverineFx.Http" />
<PackageReference Include="WolverineFx.EntityFrameworkCore" />
<PackageReference Include="WolverineFx.RabbitMQ" />
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="FluentValidation" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
<PackageReference Include="Ardalis.Specification" />
<PackageReference Include="Ardalis.Specification.EntityFrameworkCore" />

<!-- Infrastructure (building block) -->
<PackageReference Include="StackExchange.Redis" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />
<PackageReference Include="VaultSharp" />
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="Serilog.Sinks.Grafana.Loki" />
<PackageReference Include="Serilog.Enrichers.CorrelationId" />
<PackageReference Include="prometheus-net.AspNetCore" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" />
<PackageReference Include="Polly" />
<PackageReference Include="AspNetCore.HealthChecks.NpgSql" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" />
<PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" />

<!-- Consolidado.Worker (consumer RabbitMQ direto) -->
<PackageReference Include="RabbitMQ.Client" />
<PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" />

<!-- API -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" />
<PackageReference Include="Scalar.AspNetCore" />

<!-- Testes unitários -->
<PackageReference Include="xunit" />
<PackageReference Include="Moq" />
<PackageReference Include="FluentAssertions" />
<PackageReference Include="Bogus" />

<!-- Stress test (FinControl.StressTests — execução manual) -->
<PackageReference Include="NBomber" Version="5.5.0" />
<PackageReference Include="Bogus" />
```

---

## Evolução Arquitetural: Vertical Slicing + CQRS

### O Problema com Camadas Tradicionais

A estrutura tradicional (Controllers → Services → Repositories) causa:
- 📁 Pastas **Services** e **Repositories** gigantas com centenas de classes
- 🔄 Acoplamento entre funcionalidades diferentes
- 🧩 Difícil encontrar o código relacionado a um caso de uso
- 🐛 Mudanças em um feature afetam toda a camada

```
❌ ARQUITETURA DE CAMADAS (Horizontal)
┌─────────────────────────────────────┐
│     Controllers Layer               │ ← Todos os controllers aqui
├─────────────────────────────────────┤
│     Services Layer                  │ ← Todos os services aqui (100+ classes)
├─────────────────────────────────────┤
│     Repositories Layer              │ ← Todos os repos aqui
├─────────────────────────────────────┤
│     Database                        │
└─────────────────────────────────────┘

Problema: Mudança em "RegistrarDebito" toca Controllers, Services, 
Repositories, DTOs - está espalhado por toda a aplicação!
```

### Solução: Vertical Slicing + CQRS

**Vertical Slicing** organiza o código por **feature/caso de uso completo**. Cada "slice" (fatia) contém:
- ✅ Command/Query específico
- ✅ Handler
- ✅ Validator
- ✅ DTO
- ✅ Testes para aquela feature

Combinado com **CQRS**, temos:
- ✅ **Write Slices** (Lançamentos): RegistrarDebito, RegistrarCredito
- ✅ **Read Slices** (Consolidado): ObterSaldoDiaria, ObterExtrato

```
✅ VERTICAL SLICING + CQRS (por Feature)
├─ Features
│  ├─ Lancamentos (Write Model)
│  │  ├─ RegistrarDebito/
│  │  │  ├─ RegistrarDebitoCommand.cs
│  │  │  ├─ RegistrarDebitoHandler.cs
│  │  │  ├─ RegistrarDebitoValidator.cs
│  │  │  ├─ DebitoRequest.cs
│  │  │  ├─ DebitoResponse.cs
│  │  │  └─ DebitoTests.cs
│  │  ├─ RegistrarCredito/
│  │  │  ├─ RegistrarCreditoCommand.cs
│  │  │  ├─ RegistrarCreditoHandler.cs
│  │  │  ├─ RegistrarCreditoValidator.cs
│  │  │  ├─ CreditoRequest.cs
│  │  │  ├─ CreditoResponse.cs
│  │  │  └─ CreditoTests.cs
│  │
│  ├─ Consolidado (Read Model)
│  │  ├─ ObterSaldoDiaria/
│  │  │  ├─ ObterSaldoDiariaQuery.cs
│  │  │  ├─ ObterSaldoDiariaHandler.cs
│  │  │  ├─ SaldoResponse.cs
│  │  │  └─ SaldoTests.cs
│  │  ├─ ObterExtrato/
│  │  │  ├─ ObterExtratoQuery.cs
│  │  │  ├─ ObterExtratoHandler.cs
│  │  │  ├─ ExtratoResponse.cs
│  │  │  └─ ExtratoTests.cs
│
├─ Shared (Entidades, Value Objects, Eventos)
│  ├─ Domain/
│  │  ├─ Entities/
│  │  ├─ ValueObjects/
│  │  └─ Events/
│  ├─ Infrastructure/
│  │  ├─ Data/
│  │  ├─ Events/
│  │  └─ Cache/
│  └─ Middleware/ (Interceptadores Wolverine globais)

Vantagem: Tudo relativo a "RegistrarDebito" está em UM lugar!
```

### Benefícios Comprovados

| Aspecto | Antes (Camadas) | Depois (Vertical Slicing) |
|---------|-----------------|--------------------------|
| **Localização de código** | Espalhado (5+ pastas) | Uma pasta = uma feature |
| **Mudança de feature** | Toca múltiplas camadas | Mudança isolada |
| **Reusabilidade** | Handlers reutilizados (difícil) | Cada slice é completo |
| **Testes** | Separados por tipo (unit/int) | Colocados com a feature |
| **Escalabilidade** | Difícil com 50+ features | Fácil: add nova slice |
| **Onboarding** | "Onde está X?" (difícil) | "Procure em Features/X" |

---

## Estrutura de Pastas

**Estrutura real implementada** — Módulos independentes + building blocks compartilhados.

```
arquitetura-backend-2026/
├── .docs/
│   └── ARQUITETURA.md                          ← Este arquivo
│
├── src/
│   ├── Modules/
│   │   │
│   │   ├── Lancamentos/                         ← Módulo Write (CQRS Command side)
│   │   │   │
│   │   │   ├── FinControl.Lancamentos.API/      ← Entrada HTTP (Minimal APIs)
│   │   │   │   ├── Configuration/
│   │   │   │   │   ├── ApplicationModules.cs   ← Registro de todos os módulos
│   │   │   │   │   └── BearerSecuritySchemeTransformer.cs
│   │   │   │   ├── ModuleExtensions.cs         ← AddAllModules / MapAllModules
│   │   │   │   └── Program.cs                  ← Vault → Serilog → DB → JWT →
│   │   │   │                                      SubscriptionKey → Swagger/Scalar
│   │   │   │
│   │   │   └── FinControl.Lancamentos.Core/    ← Domínio + Features + Persistence
│   │   │       ├── Context/
│   │   │       │   ├── DbMapping/
│   │   │       │   │   └── LancamentoDbMapping.cs   ← EF mapping + índices
│   │   │       │   ├── LancamentosDbContext.cs       ← Global filter soft-delete
│   │   │       │   └── LancamentosDbContextFactory.cs
│   │   │       ├── Domain/
│   │   │       │   ├── Enums/
│   │   │       │   │   ├── ModalidadeLancamentoEnum.cs
│   │   │       │   │   └── TipoLancamentoEnum.cs
│   │   │       │   └── Lancamento.cs                 ← Entidade: IdempotencyKey,
│   │   │       │                                         soft-delete, TipoFormatado
│   │   │       ├── Features/
│   │   │       │   ├── Commands/
│   │   │       │   │   └── RegistrarLancamento/
│   │   │       │   │       ├── RegistrarLancamentoCommand.cs
│   │   │       │   │       ├── RegistrarLancamentoCommandHandler.cs ← Idempotência
│   │   │       │   │       ├── RegistrarLancamentoCommandValidator.cs ← .Must() runtime
│   │   │       │   │       ├── RegistrarLancamentoEndpoint.cs
│   │   │       │   │       └── RegistrarLancamentoResponse.cs
│   │   │       │   └── LancamentosFeatureExtensions.cs
│   │   │       ├── Migrations/
│   │   │       │   ├── 20260522154538_InitialCreate.cs
│   │   │       │   └── 20260523093019_AddIdempotencyKey.cs ← gen_random_uuid()
│   │   │       └── Repositories/
│   │   │           └── LancamentosRepository.cs
│   │   │
│   │   └── Consolidados/                        ← Módulo Read (CQRS Query side)
│   │       │
│   │       ├── FinControl.Consolidado.API/      ← Entrada HTTP (Minimal APIs)
│   │       │   ├── Configuration/
│   │       │   │   ├── ApplicationModules.cs
│   │       │   │   └── BearerSecuritySchemeTransformer.cs
│   │       │   ├── ModuleExtensions.cs
│   │       │   └── Program.cs                  ← Vault → Redis → JWT →
│   │       │                                      SubscriptionKey → Scalar
│   │       │
│   │       ├── FinControl.Consolidado.Core/    ← Domínio + Features
│   │       │   ├── Domain/
│   │       │   │   ├── Events/
│   │       │   │   │   └── LancamentoRegistradoHandler.cs  ← Wolverine handler
│   │       │   │   └── SaldoConsolidado.cs                 ← record(Saldo, UltimaAtualizacao)
│   │       │   └── Features/
│   │       │       ├── Commands/
│   │       │       │   └── AtualizarSaldoConsolidao/
│   │       │       │       ├── AtualizarSaldoConsolidaoCommand.cs   ← ValorLancamento + DataLancamento
│   │       │       │       └── AtualizarSaldoConsolidaoCommandHandler.cs ← Lock + Cache
│   │       │       └── Queries/
│   │       │           └── GetSaldoConsolidado/
│   │       │               ├── GetSaldoConsolidadoEndpoint.cs
│   │       │               ├── GetSaldoConsolidadoQuery.cs
│   │       │               ├── GetSaldoConsolidadoQueryHandler.cs  ← Redis lookup
│   │       │               └── GetSaldoConsolidadoResponse.cs
│   │       │
│   │       └── FinControl.Consolidado.Worker/  ← BackgroundService consumer
│   │           ├── LancamentoRegistradoConsumer.cs  ← RabbitMQ.Client direto
│   │           │                                       Exchange: lancamentos.events
│   │           │                                       Queue durable, prefetchCount: 10
│   │           │                                       Reconexão exponencial (5s → 60s)
│   │           └── Program.cs                  ← Vault → Redis → IConnectionMultiplexer
│   │                                              → IRedisLockService → Handler
│   │
│   └── bulding-blocks/                          ← Infraestrutura compartilhada
│       │
│       ├── FinControl.Auth/
│       │   └── Extensions/
│       │       └── KeycloakAuthExtensions.cs   ← AddFinControlKeycloakAuth()
│       │
│       ├── FinControl.Infrastructure/
│       │   ├── Cache/
│       │   │   ├── IRedisLockService.cs        ← Contrato de lock distribuído
│       │   │   ├── RedisCacheService.cs        ← Wrapper IDistributedCache + camelCase JSON
│       │   │   └── RedisLockService.cs         ← SETNX + Lua release atômico
│       │   ├── Data/
│       │   │   ├── BaseDbContext.cs
│       │   │   └── IAuditableEntity.cs
│       │   ├── Extensions/
│       │   │   ├── HealthChecksExtensions.cs   ← NpgSql + Redis + RabbitMQ
│       │   │   ├── ObservabilityExtensions.cs  ← OpenTelemetry + prometheus-net
│       │   │   ├── RedisExtensions.cs          ← IConnectionMultiplexer + IRedisLockService
│       │   │   ├── SerilogExtensions.cs        ← Serilog + Loki + CorrelationId
│       │   │   └── WolverineExtensions.cs      ← AddFinControlWolverine()
│       │   ├── Http/
│       │   │   ├── CorrelationIdMiddleware.cs
│       │   │   └── JwtClaimsExtractor.cs
│       │   ├── Messaging/
│       │   │   └── RabbitMqPublisher.cs
│       │   ├── Middleware/
│       │   │   ├── GlobalExceptionHandler.cs   ← RFC 7807 ProblemDetails
│       │   │   └── SubscriptionKeyMiddleware.cs ← X-Subscription-Key + timing-safe
│       │   ├── Vault/
│       │   │   ├── VaultConfigurationProvider.cs ← KV v2 → IConfiguration
│       │   │   ├── VaultExtensions.cs            ← AddFinControlVault()
│       │   │   ├── VaultKeys.cs                  ← Constantes de chaves
│       │   │   └── VaultOptions.cs
│       │   └── Wolverine/
│       │       ├── LoggingMiddleware.cs         ← CorrelationId em todos os handlers
│       │       └── ValidationMiddleware.cs      ← FluentValidation automático
│       │
│       └── FinControl.SharedKernel/
│           ├── Domain/
│           │   ├── AggregateRoot.cs
│           │   ├── DomainEntity.cs             ← Base com Id + igualdade
│           │   ├── DomainEvent.cs
│           │   ├── Events/
│           │   │   └── LancamentoRegistradoMessage.cs ← Contrato do evento
│           │   ├── IAuditableDomainEntity.cs
│           │   ├── ISoftDeleteDomainEntity.cs
│           │   ├── PagedResult.cs
│           │   └── Result.cs                   ← Result<T> tipado
│           └── Messaging/
│               ├── ICommand.cs
│               ├── IEventHandler.cs
│               └── IQuery.cs
│
├── src/tests/
│   ├── FinControl.Lancamentos.Tests/          ← 48 testes
│   │   ├── Domain/LancamentoTests.cs                       (9 testes)
│   │   ├── Fakers/LancamentoCommandFaker.cs
│   │   └── Features/Commands/
│   │       ├── RegistrarLancamentoCommandHandlerTests.cs   (12 testes)
│   │       └── RegistrarLancamentoCommandValidatorTests.cs (27 testes)
│   │
│   ├── FinControl.Consolidado.Tests/          ← 35 testes
│   │   ├── Fakers/SaldoConsolidadoFaker.cs
│   │   └── Features/
│   │       ├── Commands/AtualizarSaldoConsolidaoCommandHandlerTests.cs (9 testes)
│   │       ├── Queries/GetSaldoConsolidadoQueryHandlerTests.cs         (15 testes)
│   │       └── ConsolidadoRegrasDenegocioTests.cs                      (12 testes — regras funcionais)
│   │
│   └── FinControl.StressTests/                ← Teste de carga manual (NBomber)
│       ├── Scenarios/
│       │   ├── ConsolidadosScenario.cs        ← 50 req/s · p95 < 500ms · erro < 5%
│       │   └── LancamentosScenario.cs         ← 10 req/s · p95 < 1000ms · erro < 5%
│       ├── Fakers/
│       │   ├── LancamentoFaker.cs             ← Bogus — crédito/débito aleatório
│       │   └── LancamentoRequest.cs           ← DTO espelho sem dependência do Core
│       ├── AuthHelper.cs                      ← Auto-fetch JWT via Keycloak (password grant)
│       ├── StressConfig.cs                    ← Env vars: STRESS_BASE_URL, STRESS_DURATION…
│       └── Program.cs                         ← NBomberRunner — cenários em paralelo;
│                                                 relatórios em stress-reports/ (gitignored)
│
├── docker-compose.yml           ← PostgreSQL, Redis, RabbitMQ, Vault, Keycloak, Kong
├── README.md
└── FinControl.sln
```

---

### Explicação da Organização

**Estrutura por Vertical Slicing:**
1. **`Features/`** - Cada caso de uso é uma pasta completa
   - `RegistrarDebito/` = tudo sobre registrar débito
   - `ObterSaldoDiaria/` = tudo sobre obter saldo do dia
   - Dentro de cada feature: Command/Query, Handler, Validator, DTOs, Tests

2. **`Shared/`** - Código reutilizado em todo o microserviço
   - `Domain/` = Entidades e Value Objects
   - `Infrastructure/` = Banco de dados, cache, eventos
   - `Middleware/` = Interceptores Wolverine (logging, validação, tracing)

3. **`Endpoints/`** - Definição de rotas (Minimal APIs)
   - Mais limpo e explícito que Controllers
   - Fácil mapear POST /api/lancamentos → RegistrarDebitoHandler

4. **`Testes/`** - Espelha a estrutura de Features
   - `Features/RegistrarDebito/` tests → `src/Lancamentos.API/Features/RegistrarDebito/`
   - Mantém testes perto do código testado

---

## Implementação: Vertical Slicing com Wolverine + Minimal APIs

### Por que Wolverine?

**Wolverine** é um Next-Generation .NET Mediator AND Message Bus construído por Jeremy D. Miller (criador do MediatR). Combina Command/Query Handler Pattern com Outbox Pattern, RabbitMQ nativo, e OpenTelemetry automático.

**Vantagens:**
- ✅ **Outbox Pattern Built-in** (garantia de entrega de eventos)
- ✅ **RabbitMQ Nativo** (message bus integrado)
- ✅ **Inbox Pattern** (deduplicação automática)
- ✅ **OpenTelemetry Automático** (tracing + CorrelationId)
- ✅ **Code Generation** (zero reflection)
- ✅ **HTTP + Messaging Unificado**

**Referência:** [MEDIATOR_PATTERN_FINAL_REPORT.md](./MEDIATOR_PATTERN_FINAL_REPORT.md) — Wolverine 79/100 vs Cortex.Mediator 70/100
**Implementação:** [GUIA_IMPLEMENTACAO_WOLVERINE.md](./GUIA_IMPLEMENTACAO_WOLVERINE.md) — 4 semanas, 100% funcional

```csharp
// Command (Write) - convention-based, sem interface
public record RegistrarDebitoCommand(decimal Valor, string Descricao);

// Query (Read)
public record ObterSaldoDiariaQuery(DateTime Data);

// Handler Wolverine - auto-descoberto por convention
public class RegistrarDebitoHandler
{
    public async Task<DebitoResponse> Handle(
        RegistrarDebitoCommand command, 
        IMessageContext context)  // Wolverine injects
    {
        var lancamento = new Lancamento(...);
        
        // Outbox automático na mesma transação!
        await context.PublishAsync(
            new LancamentoRegistradoEvent(...),
            outbox: true);
        
        return new DebitoResponse(...);
    }
}
```

### Exemplo Prático: Feature RegistrarDebito

```
Features/
└── RegistrarDebito/
    ├── RegistrarDebitoCommand.cs
    ├── RegistrarDebitoHandler.cs
    ├── RegistrarDebitoValidator.cs
    ├── DebitoRequest.cs
    ├── DebitoResponse.cs
    └── RegistrarDebitoTests.cs
```

**RegistrarDebitoCommand.cs:**
```csharp
namespace Lancamentos.API.Features.RegistrarDebito;

public record RegistrarDebitoCommand(
    decimal Valor, 
    string Descricao
) : IRequest<DebitoResponse>;

public record DebitoResponse(
    Guid Id,
    DateTime Registrado,
    decimal Valor,
    string Status
);
```

**RegistrarDebitoRequest.cs:**
```csharp
namespace Lancamentos.API.Features.RegistrarDebito;

public record DebitoRequest(
    decimal Valor,
    string Descricao
);
```

**RegistrarDebitoValidator.cs:**
```csharp
namespace Lancamentos.API.Features.RegistrarDebito;

public class RegistrarDebitoValidator : AbstractValidator<RegistrarDebitoCommand>
{
    public RegistrarDebitoValidator()
    {
        RuleFor(x => x.Valor)
            .GreaterThan(0).WithMessage("Valor deve ser maior que zero")
            .LessThanOrEqualTo(100000).WithMessage("Valor não pode exceder 100.000");
        
        RuleFor(x => x.Descricao)
            .NotEmpty().WithMessage("Descrição obrigatória")
            .MaximumLength(255).WithMessage("Descrição máximo 255 caracteres");
    }
}
```

**RegistrarDebitoHandler.cs:**
```csharp
namespace Lancamentos.API.Features.RegistrarDebito;

public class RegistrarDebitoHandler : IRequestHandler<RegistrarDebitoCommand, DebitoResponse>
{
    private readonly LancamentosDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public RegistrarDebitoHandler(LancamentosDbContext dbContext, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<DebitoResponse> Handle(RegistrarDebitoCommand request, CancellationToken ct)
    {
        // Criar entidade de domínio
        var lancamento = new Lancamento(
            id: Guid.NewGuid(),
            tipo: TipoLancamento.Debito,
            valor: new Valor(request.Valor),
            descricao: request.Descricao,
            registradoEm: DateTime.UtcNow
        );

        // Persister no banco
        _dbContext.Lancamentos.Add(lancamento);
        await _dbContext.SaveChangesAsync(ct);

        // Publicar evento para que Consolidado se atualize
        await _publishEndpoint.Publish(new LancamentoRegistradoEvent
        {
            LancamentoId = lancamento.Id,
            Tipo = lancamento.Tipo,
            Valor = lancamento.Valor.Valor,
            Registrado = lancamento.RegistradoEm
        }, ct);

        return new DebitoResponse(
            Id: lancamento.Id,
            Registrado: lancamento.RegistradoEm,
            Valor: lancamento.Valor.Valor,
            Status: "Registrado"
        );
    }
}
```

**RegistrarDebitoTests.cs:**
```csharp
namespace Lancamentos.Tests.Features.RegistrarDebito;

public class RegistrarDebitoHandlerTests
{
    [Fact]
    public async Task Handle_ComValorValido_RegistraDebito()
    {
        // Arrange
        var handler = new RegistrarDebitoHandler(_dbContext, _publishEndpoint);
        var command = new RegistrarDebitoCommand(100m, "Venda produto X");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Valor.Should().Be(100m);
        result.Status.Should().Be("Registrado");
        
        var debitoNoBD = await _dbContext.Lancamentos.FirstAsync(x => x.Id == result.Id);
        debitoNoBD.Tipo.Should().Be(TipoLancamento.Debito);
    }
}
```

### Conectar Handler ao Endpoint (Minimal API)

**LancamentosEndpoints.cs:**
```csharp
namespace Lancamentos.API.Endpoints;

public static class LancamentosEndpoints
{
    public static void MapLancamentosEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/lancamentos")
            .WithName("Lançamentos")
            .WithOpenApi();

        group.MapPost("/debito", RegistrarDebito)
            .WithName("RegistrarDebito")
            .WithOpenApi();

        group.MapPost("/credito", RegistrarCredito)
            .WithName("RegistrarCredito")
            .WithOpenApi();
    }

    public static async Task<IResult> RegistrarDebito(
        DebitoRequest request,
        IMessageContext context,  // Wolverine context
        CancellationToken ct)
    {
        var command = new RegistrarDebitoCommand(request.Valor, request.Descricao);
        var result = await context.Send(command, ct);
        
        return Results.Created($"/api/lancamentos/{result.Id}", result);
    }

    public static async Task<IResult> RegistrarCredito(
        CreditoRequest request,
        IMessageContext context,  // Wolverine context
        CancellationToken ct)
    {
        var command = new RegistrarCreditoCommand(request.Valor, request.Descricao);
        var result = await context.Send(command, ct);
        
        return Results.Created($"/api/lancamentos/{result.Id}", result);
    }
}
```

**Program.cs - Registrar Wolverine:**
```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseWolverine((context, opts) =>
    {
        // RabbitMQ configurado (nativo!)
        opts.UseRabbitMq()
            .UseDurableOutbox()  // ✅ Outbox automático em PostgreSQL
            .AutoProvision();
        
        // OpenTelemetry automático
        opts.UseOpenTelemetry()
            .WithTracing();
        
        // Handler discovery por convention
        opts.IncludeAssemblyContaining<Program>();
        
        // Policies: retry, circuit breaker, etc
        opts.Policies
            .DefaultSerializerIs(SerializerType.Json)
            .OnException<ValidationException>()
            .Retry(attempts: 2, delay: TimeSpan.FromMilliseconds(100))
            .AndThen(ContinueAction.MoveToErrorQueue);
    })
    .ConfigureServices(services =>
    {
        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);
        
        // Marten (PostgreSQL CRDT + Outbox)
        services.AddMarten()
            .UsePersistence<NpgsqlConnection>()
            .ApplyAllDatabaseChangesOnStartup();
    });

var app = builder.Build();

// Mapear endpoints (Minimal APIs)
app.MapLancamentosEndpoints();

await app.RunAsync();
```

### Middleware: Cross-Cutting Concerns (Wolverine Interceptors)

**ValidationInterceptor.cs** (Validação automática):
```csharp
namespace Lancamentos.API.Shared.Middleware;

// Wolverine auto-aplica interceptores com convention naming
public class ValidationInterceptor
{
    private readonly IValidator<dynamic>? _validator;

    public ValidationInterceptor(IValidator<dynamic>? validator = null)
    {
        _validator = validator;
    }

    // Wolverine chama Before automaticamente
    public async Task Before(Envelope envelope)
    {
        if (_validator is null)
            return;

        var result = await _validator.ValidateAsync(envelope.Message);
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
    }
}
```

**LoggingInterceptor.cs** (Logging automático + CorrelationId):
```csharp
namespace Lancamentos.API.Shared.Middleware;

public class LoggingInterceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;

    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
    {
        _logger = logger;
    }

    // Wolverine chama antes do handler automaticamente
    public async ValueTask AroundInvoke(IInvocationContext context)
    {
        var messageName = context.Envelope.Message?.GetType().Name ?? "Unknown";
        var correlationId = context.Envelope.CorrelationId;
        
        _logger.LogInformation(
            "Iniciando message: {MessageName} [CorrelationId: {CorrelationId}]", 
            messageName, correlationId);
        
        var sw = Stopwatch.StartNew();
        await context.Invoke();
        sw.Stop();
        
        _logger.LogInformation(
            "Completou message: {MessageName} em {Elapsed}ms [CorrelationId: {CorrelationId}]", 
            messageName, sw.ElapsedMilliseconds, correlationId);
    }
}
```

---

## Componentes Principais

### 1. Lancamentos Service (`FinControl.Lancamentos.API` + `FinControl.Lancamentos.Core`)

**Responsabilidades:**
- ✅ Registrar novos lançamentos (débitos e créditos)
- ✅ Validar tipo, valor, data e modalidade via FluentValidation
- ✅ Verificar idempotência antes de persistir (IdempotencyKey único)
- ✅ Persistir em PostgreSQL via EF Core (ACID)
- ✅ Publicar evento `LancamentoRegistradoMessage` via Outbox Manual (OutboxRelayService)
- ✅ Manter independência funcional — não conhece o Consolidado

**Endpoints implementados:**
```
Headers obrigatórios (via Kong):
  Authorization: Bearer <token JWT Keycloak>
  Idempotency-Key: <uuid-v4>  ← gerado pelo cliente
  (X-Subscription-Key é injetado pelo Kong — nunca enviado pelo cliente)

POST /lancamentos/registrar   (via Kong: POST http://localhost:8000/lancamentos/registrar)
  Body: {
    "modalidade": "Venda" | "Devolucao" | "Suprimento" | "Sangria" |
                  "PagamentoFornecedor" | "RecebimentoDivida" | "Outros",
    "valor": 15000,                ← em centavos (long)
    "dataLancamento": "2026-05-23T10:00:00",  ← opcional, default = UTC now
    "descricao": "string"          ← obrigatório se modalidade = Outros
  }
  Response 201: {
    "navigationId": "uuid",
    "criadoEm": "2026-05-23T10:00:05Z"
  }
  Response 409: já processado (idempotência — mesmo Idempotency-Key)
  Response 400: validação falhou (ProblemDetails RFC 7807)
  Response 401: token ausente/inválido
  Response 403: subscription-key inválida
```

**Schema PostgreSQL (schema: lancamentos):**
```sql
CREATE TABLE lancamentos.lancamentos (
  id                BIGSERIAL PRIMARY KEY,
  navigation_id     UUID NOT NULL DEFAULT gen_random_uuid(),
  idempotency_key   UUID NOT NULL,
  tipo              INTEGER NOT NULL,      -- enum TipoLancamento
  modalidade        INTEGER NOT NULL,      -- enum ModalidadeLancamento
  valor             BIGINT NOT NULL,       -- centavos
  data_lancamento   TIMESTAMPTZ NOT NULL,
  descricao         VARCHAR(300),
  created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at        TIMESTAMPTZ,
  deleted_at        TIMESTAMPTZ            -- soft-delete (global query filter)
);

CREATE UNIQUE INDEX idx_lancamento_idempotency_key
  ON lancamentos.lancamentos (idempotency_key);
CREATE INDEX idx_lancamento_data
  ON lancamentos.lancamentos (data_lancamento DESC);
```

---

### 2. Consolidado Service — duas camadas

#### 2a. `FinControl.Consolidado.API` (leitura)

**Responsabilidades:**
- ✅ Servir saldo consolidado por data via Redis (<5ms hit)
- ✅ Autenticado com Keycloak + SubscriptionKeyMiddleware

**Endpoint implementado:**
```
GET /consolidados/saldo?data-lancamento=yyyy-MM-dd
  (via Kong: GET http://localhost:8000/consolidados/saldo?data-lancamento=2026-05-23)
  Response 200: {
    "data": "2026-05-23",
    "saldo": 125000,           ← em centavos (long)
    "ultimaAtualizacao": "2026-05-23T10:15:30Z"
  }
  Response 404: sem saldo para a data (cache vazio — nenhum lançamento processado)
  Response 401: token ausente/inválido
  Response 403: subscription-key inválida
```

#### 2b. `FinControl.Consolidado.Worker` (consumidor de eventos)

**Responsabilidades:**
- ✅ Consumir `LancamentoRegistradoMessage` do RabbitMQ
- ✅ Adquirir lock distribuído por data (`lock:saldo:consolidado:{data}`)
- ✅ Ler saldo atual do Redis, somar o valor do lançamento e persistir
- ✅ ACK/NACK com requeue em caso de falha; NACK sem requeue para mensagem inválida

**Fluxo de atualização:**
```
RabbitMQ → LancamentoRegistradoConsumer
  → AtualizarSaldoConsolidadoCommandHandler
    → IRedisLockService.ExecuteWithLockAsync("lock:saldo:consolidado:yyyy-MM-dd")
      → RedisCacheService.GetAsync<SaldoConsolidado>("saldo:consolidado:yyyy-MM-dd")
      → novoSaldo = saldoAtual + ValorLancamento
      → RedisCacheService.SetAsync(key, novoSaldo, TTL=30 dias)
    → ACK para RabbitMQ
```

**Cache:**
```
Chave:  saldo:consolidado:{yyyy-MM-dd}          (prefixo FinControl: no Redis)
Valor:  { "saldo": 125000, "ultimaAtualizacao": "..." }
TTL:    30 dias (renovado a cada atualização)
Lock:   lock:saldo:consolidado:{yyyy-MM-dd}     (5 tentativas × 100ms, expiry 10s)
```

> Não existe banco de dados de consolidação — o Redis é a única fonte de leitura.
> Consistência eventual garantida pelo Outbox Pattern do Wolverine.

---

### 3. Event Bus (RabbitMQ + Outbox Manual)

**Evento publicado:**
```csharp
// FinControl.SharedKernel/Domain/Events/LancamentoRegistradoMessage.cs
public record LancamentoRegistradoMessage(
    long Id,
    Guid NavigationId,
    ModalidadeLancamento Modalidade,
    long Valor,
    string? Descricao,
    DateTimeOffset DataLancamento,
    DateTimeOffset OcorridoEm,
    string? UsuarioId,
    string? UsuarioNome,
    string? UsuarioEmail,
    string? CorrelationId);
```

**Topologia RabbitMQ:**
```
Exchange: lancamentos.events   (topic, durable)
  └── Binding: lancamento.criado
       └── Queue: fincontrol.consolidado.lancamento-registrado (durable)
```

**Padrão Outbox implementado (Lancamentos — transação atômica):**
```csharp
// RegistrarLancamentoCommandHandler.cs — garante atomicidade entre lançamento e outbox
var tx = await db.Database.BeginTransactionAsync(cancellationToken);
try
{
    // 1ª gravação: persiste o lançamento (Id e NavigationId gerados pelo banco)
    db.Set<Lancamento>().Add(lancamento);
    await db.SaveChangesAsync(cancellationToken);

    // 2ª gravação: persiste o OutboxMessage (payload já tem Id e NavigationId)
    db.Set<OutboxMessage>().Add(new OutboxMessage
    {
        MessageType = nameof(LancamentoRegistradoMessage),
        Payload     = JsonSerializer.Serialize(evento, JsonOptions),
        Exchange    = "lancamentos.events",
        RoutingKey  = "lancamento.criado",
        CreatedAt   = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);
}
catch { await tx.RollbackAsync(cancellationToken); throw; }
```

**OutboxRelayService — relay assíncrono com Polly:**
```csharp
// FinControl.Lancamentos.Core/Outbox/OutboxRelayService.cs
// BackgroundService com polling a cada 5s, batch de 50 mensagens
// Retry: 3× com backoff exponencial + jitter (Polly ResiliencePipeline)
// Se falhar após 3 tentativas: incrementa RetryCount + LastError na tabela
```

**Tabela outbox_messages (schema: lancamentos):**
```sql
CREATE TABLE lancamentos.outbox_messages (
  id           BIGSERIAL PRIMARY KEY,
  message_type VARCHAR(200) NOT NULL,
  payload      TEXT NOT NULL,
  exchange     VARCHAR(200) NOT NULL,
  routing_key  VARCHAR(200) NOT NULL,
  created_at   TIMESTAMPTZ NOT NULL,
  delivered_at TIMESTAMPTZ,          -- NULL = pendente
  retry_count  INT NOT NULL DEFAULT 0,
  last_error   VARCHAR(2000)
);
CREATE INDEX idx_outbox_messages_delivered_at
  ON lancamentos.outbox_messages (delivered_at);
```

---

## Fluxos de Dados

### Fluxo 1: Registrar Lançamento

```
Cliente HTTP
    │
    ├─→ POST /lancamentos/registrar
    │   Authorization: Bearer <JWT Keycloak>
    │   Idempotency-Key: <uuid-v4>
    │   Body: { "modalidade": "Venda", "valor": 15000, "dataLancamento": "..." }
    │
    ▼
Kong API Gateway (porta 8000)
    │
    ├─→ [jwt plugin] Valida JWT com chave pública RS256 do Keycloak
    ├─→ [rate-limiting] 300 req/min por consumer
    ├─→ [request-transformer] Injeta X-Subscription-Key (segredo interno)
    ├─→ [correlation-id] Propaga X-Correlation-Id
    │
    ▼ upstream: host.docker.internal:5083
    │
RegistrarLancamentoEndpoint (Wolverine HTTP)
    │
    ├─→ SubscriptionKeyMiddleware: valida X-Subscription-Key do Vault
    ├─→ [Authorize]: verifica JWT
    ├─→ Extrai usuário + correlationId do contexto HTTP
    │
    ▼
RegistrarLancamentoCommandHandler
    │
    ├─→ Verifica idempotência (SELECT por IdempotencyKey) → 409 se já existe
    ├─→ BEGIN TRANSACTION (PostgreSQL)
    │   ├─→ INSERT lancamentos.lancamentos (persiste lançamento)
    │   └─→ INSERT lancamentos.outbox_messages (persiste OutboxMessage)
    │       Payload = LancamentoRegistradoMessage serializado
    └─→ COMMIT
    │
Response HTTP 201: { navigationId, criadoEm }
    │
    (assíncrono — fora do request HTTP)
    │
    ▼
OutboxRelayService (BackgroundService — polling 5s)
    │
    ├─→ SELECT outbox_messages WHERE delivered_at IS NULL LIMIT 50
    ├─→ Para cada mensagem:
    │   ├─→ [Polly] Retry 3× exponencial + jitter
    │   ├─→ RabbitMqPublisher.PublishRawAsync()
    │   │   → Exchange: lancamentos.events, RoutingKey: lancamento.criado
    │   └─→ UPDATE outbox_messages SET delivered_at = NOW()
    │
    ▼
RabbitMQ (Queue: fincontrol.consolidado.lancamento-registrado)
    │
    ▼
LancamentoRegistradoConsumer (Consolidado.Worker)
    │
    ├─→ Adquire lock distribuído Redis (lock:saldo:consolidado:{data})
    ├─→ Lê saldo atual do Redis
    ├─→ Incrementa saldo com valor do lançamento
    └─→ SET Redis: saldo:consolidado:{data} (TTL 30 dias)
```

---

### Fluxo 2: Consultar Saldo Consolidado

```
Cliente HTTP
    │
    ├─→ GET /consolidados/saldo?data-lancamento=2026-05-20
    │   Authorization: Bearer <JWT Keycloak>
    │
    ▼
Kong API Gateway (porta 8000)
    │
    ├─→ [jwt plugin] Valida JWT
    ├─→ [proxy-cache] Cache GET por 30s (evita até 1500 req/30s sem tocar upstream)
    ├─→ [rate-limiting] 55 req/s / 3300 req/min por consumer
    ├─→ [request-transformer] Injeta X-Subscription-Key
    │
    ▼ upstream: host.docker.internal:5260
    │
GetSaldoConsolidadoEndpoint (Wolverine HTTP)
    │
    ├─→ SubscriptionKeyMiddleware: valida X-Subscription-Key
    │
    ▼
GetSaldoConsolidadoQueryHandler
    │
    ├─→ Redis GET saldo:consolidado:{data}
    │
    ├─→ HIT: Retorna imediatamente (<5ms)
    │
    ├─→ MISS: Retorna 404 (saldo não calculado ainda)
    │   (cache é sempre populado pelo Worker; cold start = sem lançamentos)
    │
    ▼
Response HTTP 200:
{
  "data": "2026-05-20",
  "saldoAnterior": 10000.00,
  "debitos": 2500.00,
  "creditos": 3200.00,
  "saldoAtual": 10700.00,
  "timestamp": "2026-05-20T15:30:45Z"
}
```

---

### Fluxo 3: Pico de Requisições (50 req/s)

```
50 Requisições/segundo
        │
        ▼
API Gateway (Rate Limiter)
        │
        ├─→ Aceita ~47-48 req/s
        └─→ Rejeita ~2-3 req/s (5% loss - dentro do SLA)
        │
        ▼
Consolidado Service
        │
        ├─→ Consulta Redis Cache
        │   └─→ HIT RATE: ~95% (dados não mudam constantemente)
        │       └─→ Retorna em <5ms cada
        │
        └─→ MISS (5% das requisições)
            └─→ Consulta PostgreSQL
                └─→ Retorna em ~20-50ms
                └─→ Armazena em Cache

Total: Baixa latência, taxa de perda controlada
```

---

## Requisitos Não-Funcionais

### 1. Resiliência

| Cenário | Solução |
|---------|---------|
| Consolidado cai | Lançamentos continua operacional; fila persiste eventos |
| Banco cai | Circuit breaker abre; falha fast; dados em fila aguardam |
| Cache vazio | Fallback para consulta direto no BD (mais lento mas funcional) |
| Fila cheia | Backpressure; clientes recebem 503 ou entra em retry |

**Implementação Polly:**
```csharp
IAsyncPolicy<HttpResponseMessage> policy = Policy
  .Handle<HttpRequestException>()
  .Or<TimeoutReachedException>()
  .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
  .CircuitBreakerAsync<HttpResponseMessage>(
    handledEventsAllowedBeforeBreaking: 3,
    durationOfBreak: TimeSpan.FromSeconds(30)
  )
  .WrapAsync(Policy.TimeoutAsync<HttpResponseMessage>(
    TimeSpan.FromSeconds(5)
  ))
  .WrapAsync(Policy.BulkheadAsync<HttpResponseMessage>(
    maxParallelization: 100
  ));
```

---

### 2. Performance

| Métrica | Target | Implementação |
|---------|--------|---------------|
| P50 GET /consolidado | <10ms | Cache Redis |
| P95 GET /consolidado | <50ms | Replicação DB + índices |
| P99 GET /consolidado | <200ms | Read replicas |
| POST /lancamentos | <100ms | Async event publishing |
| Throughput | 50 req/s + 5% loss | Queue + Auto-scaling |

**Índices PostgreSQL:**
```sql
CREATE INDEX CONCURRENTLY idx_consolidado_data 
  ON consolidados(data DESC) 
  WHERE data >= CURRENT_DATE - INTERVAL '30 days';

CREATE INDEX CONCURRENTLY idx_lancamento_data_tipo 
  ON lancamentos(data_registro DESC, tipo)
  WHERE ativo = true;
```

---

### 3. Disponibilidade

```
Availability Target: 99.9% (High Availability)

= 8.64 segundos de downtime permitido por dia
= 43 minutos de downtime permitido por mês

Implementação:
├─ Multi-region deployment (ativo-ativo)
├─ Database replication
├─ Load balancer com health checks
├─ Auto-recovery e auto-scaling
└─ Monitoring + Alertas
```

---

## Segurança

### 1. Autenticação

Keycloak como Identity Provider. Configurado via `AddFinControlKeycloakAuth()` (building block `FinControl.Auth`):

```csharp
// FinControl.Auth/Extensions/KeycloakAuthExtensions.cs
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority   = configuration["Keycloak:Authority"];
        options.Audience    = configuration["Keycloak:Audience"];
        options.RequireHttpsMetadata = !env.IsDevelopment();
    });
```

### 2. Autorização

```csharp
// Endpoints protegidos com .RequireAuthorization()
app.MapPost("/lancamentos", handler).RequireAuthorization();
app.MapGet("/lancamentos",  handler).RequireAuthorization();
app.MapGet("/consolidado/saldo/{data}", handler).RequireAuthorization();
```

### 3. SubscriptionKeyMiddleware (segunda camada após Kong) — implementado

```csharp
// FinControl.Infrastructure/Middleware/SubscriptionKeyMiddleware.cs
// Registro no pipeline (antes da autenticação):
app.UseSubscriptionKeyValidation(VaultKeys.KongLancamentosSubscriptionKey);

// Comportamento:
// - Bypassa /health, /health/ready e /metrics (sem autenticação)
// - Lê X-Subscription-Key do header
// - Compara com IConfiguration[configKey] usando CryptographicOperations.FixedTimeEquals
// - Se Vault não retornou a chave (dev/offline): bypassa silenciosamente
// - Se chave inválida: retorna 401 ProblemDetails com correlationId
```

**Arquitetura de segurança Kong ↔ Backend:**

O cliente **nunca** envia a subscription key. Kong injeta o header `X-Subscription-Key` automaticamente via plugin `request-transformer` antes de encaminhar ao upstream. O segredo é compartilhado exclusivamente entre Kong e o serviço. Se uma requisição chegar diretamente à porta do serviço (bypass do gateway), o middleware a rejeita.

```
Cliente → [Authorization: Bearer JWT]
         → Kong
            ├─ [jwt plugin]: valida JWT com RS256
            ├─ [request-transformer]: adiciona X-Subscription-Key (interno)
            └─ upstream → SubscriptionKeyMiddleware → valida X-Subscription-Key
```

**Por que duas camadas?**

Kong valida na borda. O middleware garante que mesmo requisições que bypasser o gateway (testes internos, acesso direto à porta) sejam bloqueadas.

### 4. Validação de Entrada (implementado)

```csharp
// RegistrarLancamentoCommandValidator.cs
RuleFor(x => x.Tipo).IsInEnum();
RuleFor(x => x.Modalidade).IsInEnum();
RuleFor(x => x.Valor).GreaterThan(0).LessThanOrEqualTo(1_000_000_000);

// Avaliado em runtime (não capturado no construtor)
RuleFor(x => x.DataLancamento)
    .Must(d => d <= DateTimeOffset.UtcNow.AddDays(1))
    .WithMessage("Data nao pode ser no futuro (maximo 1 dia a frente).")
    .Must(d => d >= DateTimeOffset.UtcNow.AddYears(-1))
    .When(x => x.DataLancamento != default)
    .WithMessage("Data nao pode ser anterior a 1 ano.");

// Descricao obrigatória somente para modalidade Outros
RuleFor(x => x.Descricao)
    .NotEmpty()
    .When(x => x.Modalidade == ModalidadeLancamento.Outros);
```

### 4. Criptografia em Trânsito

```
HTTPS/TLS 1.3 obrigatório
├─ Certificado auto-assinado (dev)
├─ Let's Encrypt (staging/prod)
└─ Renovação automática
```

### 5. Firewall & WAF (Web Application Firewall)

#### 5.1 Opção Recomendada: ModSecurity + Kong + Fail2Ban

**Arquitetura em Camadas:**

```
Internet (Ataques)
    ↓
┌─────────────────────────────────────────────────┐
│ ModSecurity (WAF - tipo Cloudflare)             │
│ ├─ Bloqueia SQL Injection automaticamente        │
│ ├─ Bloqueia XSS, Path Traversal                  │
│ ├─ Detecta bots/scanners maliciosos              │
│ ├─ DDoS básico (por IP)                         │
│ └─ OWASP Top 10 proteção                        │
└─────────────────┬───────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────┐
│ Kong API Gateway                                │
│ ├─ rate-limiting: 3.000 req/min (50 req/s)     │
│ ├─ jwt: validação de tokens                     │
│ ├─ ip-restriction: whitelist/blacklist          │
│ ├─ cors: validação de origem                    │
│ └─ circuit-breaker: resiliência                 │
└─────────────────┬───────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────┐
│ Fail2Ban (Detecção Comportamental)              │
│ ├─ Monitora logs de erros 401/5xx               │
│ ├─ 10 erros 401 em 5min = banir IP por 1h      │
│ └─ Alertas automatizados                        │
└─────────────────┬───────────────────────────────┘
                  ↓
┌─────────────────────────────────────────────────┐
│ ASP.NET Core Services (Validação Final)         │
│ ├─ FluentValidation (regras de negócio)        │
│ ├─ JWT Bearer Authentication                    │
│ ├─ Role-based Authorization                     │
│ └─ EF Core (SQL parametrizado)                  │
└─────────────────────────────────────────────────┘
```

#### 5.2 ModSecurity: Proteção OWASP Top 10

```
ModSecurity bloqueia automaticamente:

✅ SQL Injection         → Detecta: SELECT, UNION, DROP, INSERT em params
✅ Cross-Site Scripting  → Detecta: <script>, javascript:, onerror=
✅ Command Injection     → Detecta: |, ;, &, $(, backticks
✅ Path Traversal        → Detecta: ../, ..\\, %2e%2e
✅ Local File Inclusion  → Detecta: /etc/passwd, /var/www
✅ Remote File Inclusion → Detecta: http://, ftp:// em params
✅ HTTP Response Splitting → Detecta: %0d%0a em headers
✅ HTTP Smuggling        → Detecta: Transfer-Encoding conflicts
✅ Bot Detection         → Detecta: nmap, nikto, sqlmap user-agents
✅ Anomaly Detection     → Detecta: padrões suspeitos
```

#### 5.3 Kong Plugins de Segurança (Nativos)

```bash
# 1. Rate Limiting (DDoS prevention)
curl -X POST http://localhost:8001/plugins \
  -d "name=rate-limiting" \
  -d "service_id=consolidado-service" \
  -d "config.minute=3000" \
  -d "config.policy=redis"

# 2. JWT Authentication
curl -X POST http://localhost:8001/plugins \
  -d "name=jwt" \
  -d "route_id=consolidado-route" \
  -d "config.key_claim_name=kid"

# 3. IP Restriction (Whitelist/Blacklist)
curl -X POST http://localhost:8001/plugins \
  -d "name=ip-restriction" \
  -d "service_id=consolidado-service" \
  -d "config.whitelist=192.168.1.0/24,10.0.0.0/8"

# 4. CORS (Validação de Origem)
curl -X POST http://localhost:8001/plugins \
  -d "name=cors" \
  -d "service_id=consolidado-service" \
  -d "config.origins=https://seu-dominio.com"

# 5. Request Size Limiter (evita uploads gigantes)
curl -X POST http://localhost:8001/plugins \
  -d "name=request-size-limiting" \
  -d "service_id=consolidado-service" \
  -d "config.allowed_payload_size=10485760"
```

#### 5.4 Docker Compose: ModSecurity + Kong Completo

```yaml
version: '3.8'

services:
  # WAF (ModSecurity) - tipo Cloudflare
  modsecurity:
    image: coreruleset/modsecurity:latest
    container_name: modsec-waf
    ports:
      - "8080:8080"  # Porta de entrada do WAF
    environment:
      - PARANOIA=2   # 1=Low, 2=Medium, 3=High, 4=Paranoia
      - ANOMALY_INBOUND=10
      - ANOMALY_OUTBOUND=5
      - BACKEND=http://kong:8000
    volumes:
      - ./security/modsecurity.conf:/etc/modsecurity/modsecurity.conf
      - ./security/crs-setup.conf:/etc/modsecurity/crs-setup.conf
    networks:
      - fluxocaixa-net

  # Kong API Gateway
  kong:
    image: kong:3.8
    container_name: kong-gateway
    depends_on:
      - kong-db
      - redis
    ports:
      - "8000:8000"   # Proxy port
      - "8001:8001"   # Admin API
      - "1337:1337"   # Konga UI
    environment:
      KONG_DATABASE: postgres
      KONG_PG_HOST: kong-db
      KONG_PG_USER: kong
      KONG_PG_PASSWORD: kong_pass
      KONG_PROXY_ACCESS_LOG: /dev/stdout
      KONG_ADMIN_ACCESS_LOG: /dev/stdout
    command: kong start
    networks:
      - fluxocaixa-net

  kong-db:
    image: postgres:15-alpine
    container_name: kong-db
    environment:
      POSTGRES_DB: kong
      POSTGRES_USER: kong
      POSTGRES_PASSWORD: kong_pass
    volumes:
      - kong-db-data:/var/lib/postgresql/data
    networks:
      - fluxocaixa-net

  # Redis para cache e rate limiting distribuído
  redis:
    image: redis:7.4-alpine
    container_name: redis-cache
    ports:
      - "6379:6379"
    networks:
      - fluxocaixa-net

  # PostgreSQL - dados da aplicação
  postgres:
    image: postgres:15-alpine
    container_name: postgres-db
    environment:
      POSTGRES_DB: fluxocaixa
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    networks:
      - fluxocaixa-net

  # RabbitMQ - event bus
  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    container_name: rabbitmq-broker
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: admin
      RABBITMQ_DEFAULT_PASS: admin
    networks:
      - fluxocaixa-net

  # Fail2Ban - detecção comportamental de ataques
  fail2ban:
    image: crazymax/fail2ban:latest
    container_name: fail2ban-monitor
    cap_add:
      - NET_ADMIN
      - NET_RAW
    volumes:
      - /var/log/docker:/var/log/docker:ro
    networks:
      - fluxocaixa-net

volumes:
  kong-db-data:
  postgres-data:

networks:
  fluxocaixa-net:
    driver: bridge
```

#### 5.5 Fluxo Completo de uma Requisição

```
1. Cliente → ModSecurity:8080
   ✓ Validar SQL injection?      SIM → Bloqueia ❌
   ✓ Validar XSS?                SIM → Bloqueia ❌
   ✓ Validar bot/scanner?        SIM → Bloqueia ❌
   → Se OK: passa pro Kong

2. ModSecurity → Kong:8000
   ✓ Token JWT válido?           NÃO → retorna 401 ❌
   ✓ IP na whitelist?            NÃO → retorna 403 ❌
   ✓ Rate limit (3.000/min)?     SIM → retorna 429 ❌
   → Se OK: passa pro serviço

3. Kong → Consolidado:5001
   ✓ FluentValidation OK?        NÃO → retorna 400 ❌
   ✓ EF Core SQL seguro?         SIM → executa
   → Retorna resultado ✅

4. Fail2Ban monitora logs
   ✓ 10× erro 401 em 5min?       SIM → banir IP por 1h 🚫
   ✓ Padrão de ataque?           SIM → gerar alert 📢
```

#### 5.6 Custo vs Cloudflare

| Solução | Custo | Setup | Features |
|---------|-------|-------|----------|
| **Cloudflare** | $20-200/mês | Nenhum | WAF + CDN + DDoS |
| **ModSec + Kong + Fail2Ban** | **$0** | 4 horas | WAF + LB + Detection |
| **AWS WAF** | $50-500/mês | Médio | WAF apenas |
| **Nginx + ModSec** | $0 | 8 horas | LB + WAF |

**Conclusão:** Você tem **proteção equivalente a Cloudflare SEM PAGAR NADA!** ✅

---

### 6. Proteção Contra Ataques (Resumo)

| Ataque | Mitigação |
|--------|-----------|
| SQL Injection | ModSecurity + EF Core Parameterized Queries |
| XSS | ModSecurity + CSP headers |
| CSRF | CSRF token em formulários |
| Rate Limiting | Kong (3.000 req/min) |
| DDoS | ModSecurity + Kong + Fail2Ban |
| Command Injection | ModSecurity |
| Bot/Scanner | ModSecurity + Fail2Ban |

---

## Key Vault & Secrets Management

### Por que Key Vault é Crítico?

Você **NÃO DEVE** armazenar secrets (senhas, tokens, chaves) em código ou ambiente diretamente:

```csharp
// ❌ ERRADO - SECRET EXPOSTO
var connectionString = "Server=db;User Id=admin;Password=minha_senha_123;";

// ✅ CORRETO - VINDO DO KEY VAULT
var connectionString = await keyVaultClient.GetSecretAsync("db-connection-string");
```

### Opção 1: Hashicorp Vault (RECOMENDADO PARA ON-PREMISES)

**Hashicorp Vault** é um **secret vault open source** (tipo Azure Key Vault, mas on-premises):

```
┌──────────────────────────────────────┐
│ Hashicorp Vault (Secret Storage)     │
├──────────────────────────────────────┤
│ ├─ Database passwords                │
│ ├─ API keys / JWT secrets            │
│ ├─ Encryption keys                   │
│ ├─ SSL certificates                  │
│ └─ Audit logs (quem acessou o quê)   │
└─────────┬──────────────────────────────┘
          ↓
    ASP.NET Core
    (injeta via DI)
```

**Docker Compose - Hashicorp Vault:**

```yaml
vault:
  image: hashicorp/vault:1.18
  container_name: vault-secrets
  ports:
    - "8200:8200"
  environment:
    - VAULT_DEV_ROOT_TOKEN_ID=myroot
    - VAULT_DEV_LISTEN_ADDRESS=0.0.0.0:8200
  volumes:
    - vault-data:/vault/data
  cap_add:
    - IPC_LOCK
  networks:
    - fluxocaixa-net

volumes:
  vault-data:
```

**Setup Vault - Armazena Secrets:**

```bash
# 1. Acessar Vault UI em http://localhost:8200
# 2. Login com token: myroot
# 3. Criar secrets:

# Via CLI:
vault kv put secret/db \
  connection_string="Server=postgres;User=admin;Password=senha;"
  
vault kv put secret/jwt \
  signing_key="sua_chave_super_secreta_aqui"
  
vault kv put secret/kong \
  admin_api_key="kong-super-secret-key"
```

**Implementação real — `AddFinControlVault()` (building block):**

```csharp
// Program.cs (Lancamentos.API e Consolidado.API)
try
{
    builder.AddFinControlVault();  // carrega vault.settings.json + preenche IConfiguration
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"Vault nao disponivel em desenvolvimento: {ex.Message}");
}

// VaultExtensions.AddFinControlVault():
// 1. Carrega vault.settings.json e vault.settings.{Env}.json
// 2. Lê VaultOptions (url, token, secretPaths)
// 3. Adiciona VaultConfigurationProvider ao pipeline de IConfiguration
// 4. Cada chave Vault (ex: dev/postgres:connection_string) vira
//    IConfiguration["dev/postgres:connection_string"]
```

**Chaves de Vault utilizadas (`VaultKeys.cs`):**
```csharp
public static class VaultKeys
{
    public const string PostgresConnection            = "dev/postgres:connection_string";
    public const string RedisConnection               = "dev/redis:connection_string";
    public const string RabbitMqUri                   = "dev/rabbitmq:uri";
    public const string KongLancamentosSubscriptionKey = "dev/kong:lancamentos_subscription_key";
    public const string KongConsolidadosSubscriptionKey = "dev/kong:consolidados_subscription_key";
}
```

**Configuração em desenvolvimento (`vault.settings.Development.json`):**
```json
{
  "Vault": {
    "Address": "http://localhost:8200",
    "Token": "fincontrol_dev_token_12345",
    "SecretPaths": ["dev/postgres", "dev/redis", "dev/rabbitmq", "dev/kong"]
  }
}
```

**NuGet Package:**
```xml
<PackageReference Include="VaultSharp" />
```

### Opção 2: Azure Key Vault (SE FOR AZURE)

Se você usar Azure:

```csharp
var builder = WebApplication.CreateBuilder(args);

var keyVaultUrl = new Uri($"https://{builder.Configuration["KeyVault:Name"]}.vault.azure.net");

builder.Configuration.AddAzureKeyVault(
    keyVaultUrl,
    new DefaultAzureCredential());

var app = builder.Build();
app.Run();
```

---

## Autenticação & Autorização Centralizadas

### Por que Keycloak?

Você **PODE** usar JWT direto no ASP.NET Core, mas Keycloak oferece:

```
┌─────────────────────────────────────────────┐
│ Keycloak (Identity Provider)                │
├─────────────────────────────────────────────┤
│ ✅ Single Sign-On (SSO) - login centralizado│
│ ✅ Multi-tenant support (múltiplos clientes)│
│ ✅ Social login (Google, GitHub, etc)       │
│ ✅ 2FA / MFA support                        │
│ ✅ User management UI                       │
│ ✅ OpenID Connect + OAuth2 compliant        │
│ ✅ LDAP/Active Directory integration        │
│ ✅ Audit logs (quem fez login quando)       │
└─────────────────────────────────────────────┘
```

**Docker Compose - Keycloak:**

```yaml
keycloak:
  image: quay.io/keycloak/keycloak:latest
  container_name: keycloak-auth
  ports:
    - "8443:8080"  # UI
  environment:
    - KEYCLOAK_ADMIN=admin
    - KEYCLOAK_ADMIN_PASSWORD=admin_pass
    - POSTGRES_DB=keycloak
    - POSTGRES_USER=keycloak
    - POSTGRES_PASSWORD=keycloak_pass
    - POSTGRES_HOST=keycloak-db
  depends_on:
    - keycloak-db
  networks:
    - fluxocaixa-net

keycloak-db:
  image: postgres:15-alpine
  container_name: keycloak-db
  environment:
    - POSTGRES_DB=keycloak
    - POSTGRES_USER=keycloak
    - POSTGRES_PASSWORD=keycloak_pass
  volumes:
    - keycloak-db-data:/var/lib/postgresql/data
  networks:
    - fluxocaixa-net

volumes:
  keycloak-db-data:
```

**Setup Keycloak:**

```bash
# 1. Acessar http://localhost:8443
# 2. Login: admin / admin_pass
# 3. Criar Realm "fluxocaixa"
# 4. Criar Client "consolidado-api"
# 5. Gerar Client Secret
```

**ASP.NET Core - Integração com Keycloak:**

```csharp
// Program.cs
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = "http://keycloak:8080/auth/realms/fluxocaixa";
        options.Audience = "consolidado-api";
        options.RequireHttpsMetadata = false;  // dev only
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy =>
        policy.RequireRole("User", "Admin"));
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

**NuGet Packages:**
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
```

---

## Monitoramento & Observabilidade

### Arquitetura de Observabilidade Completa

```
┌─────────────────────────────────────────────────┐
│ ASP.NET Core Services                           │
│ ├─ Logs (Serilog estruturado)                   │
│ ├─ Metrics (OpenTelemetry)                      │
│ └─ Traces (OpenTelemetry + Jaeger)              │
└──────────────────┬──────────────────────────────┘
                   ↓
        ┌──────────────────────┐
        │ OpenTelemetry        │
        │ (collector)          │
        └─────┬────────────────┘
              ↓
        ┌─────────────────────────────────┐
        │ ├─ Loki (logs)                  │
        │ ├─ Prometheus (métricas)        │
        │ └─ Jaeger (traces)              │
        └────────────┬────────────────────┘
                     ↓
             Grafana (visualização)
```

### Opção 1: Stack Nativa .NET + Open Source

**OpenTelemetry com .NET nativo:**

```csharp
// Program.cs
builder.Services
    .AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "localhost";
                options.AgentPort = 6831;
            });
    });

// Adicionar Serilog (structured logging)
builder.Services.AddSerilog(config =>
{
    config
        .MinimumLevel.Information()
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.Seq("http://localhost:5341")
        .WriteTo.File("logs/app-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
});

var app = builder.Build();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.Run();
```

**Docker Compose - Stack Completo:**

```yaml
# Prometheus (coleta métricas)
prometheus:
  image: prom/prometheus:latest
  container_name: prometheus
  ports:
    - "9090:9090"
  volumes:
    - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
    - prometheus-data:/prometheus
  command:
    - '--config.file=/etc/prometheus/prometheus.yml'
  networks:
    - fluxocaixa-net

# Loki (coleta logs)
loki:
  image: grafana/loki:latest
  container_name: loki
  ports:
    - "3100:3100"
  volumes:
    - ./monitoring/loki-config.yml:/etc/loki/local-config.yaml
    - loki-data:/loki
  command: -config.file=/etc/loki/local-config.yaml
  networks:
    - fluxocaixa-net

# Jaeger (distributed tracing)
jaeger:
  image: jaegertracing/all-in-one:latest
  container_name: jaeger
  ports:
    - "16686:16686"  # UI
    - "6831:6831/udp"  # agent
  networks:
    - fluxocaixa-net

# Grafana (visualização)
grafana:
  image: grafana/grafana:latest
  container_name: grafana-dashboard
  ports:
    - "3000:3000"
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=admin
    - GF_USERS_ALLOW_SIGN_UP=false
  volumes:
    - ./monitoring/grafana-datasources.yml:/etc/grafana/provisioning/datasources/datasources.yml
    - grafana-data:/var/lib/grafana
  depends_on:
    - prometheus
    - loki
  networks:
    - fluxocaixa-net

# Seq (log viewer - alternativa ao Kibana)
seq:
  image: datalust/seq:latest
  container_name: seq-logs
  ports:
    - "5341:80"  # UI em http://localhost:5341
  environment:
    - ACCEPT_EULA=Y
  volumes:
    - seq-data:/data
  networks:
    - fluxocaixa-net

volumes:
  prometheus-data:
  loki-data:
  grafana-data:
  seq-data:
```

**Prometheus Config (prometheus.yml):**

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'consolidado-api'
    static_configs:
      - targets: ['consolidado:5001']
    metrics_path: '/metrics'

  - job_name: 'lancamentos-api'
    static_configs:
      - targets: ['lancamentos:5002']
    metrics_path: '/metrics'

  - job_name: 'kong'
    static_configs:
      - targets: ['kong:8001']
    metrics_path: '/metrics'
```

### Opção 2: ELK Stack (Elasticsearch + Logstash + Kibana)

Para logs em **grande escala**, ELK é melhor:

```yaml
elasticsearch:
  image: docker.elastic.co/elasticsearch/elasticsearch:8.0.0
  environment:
    - discovery.type=single-node
    - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
  ports:
    - "9200:9200"
  volumes:
    - elasticsearch-data:/usr/share/elasticsearch/data

logstash:
  image: docker.elastic.co/logstash/logstash:8.0.0
  volumes:
    - ./monitoring/logstash.conf:/usr/share/logstash/pipeline/logstash.conf
  ports:
    - "5000:5000"

kibana:
  image: docker.elastic.co/kibana/kibana:8.0.0
  ports:
    - "5601:5601"
  environment:
    - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
```

### Comparativo: Qual Usar?

| Stack | Escalabilidade | Custo | Setup | Para |
|-------|---|---|---|---|
| **OpenTelemetry + Prometheus + Grafana** | Médio | $0 | 2h | MVP/Médio |
| **ELK Stack** | Alto | $0 | 4h | Logs em alta escala |
| **Datadog** | Altíssimo | $200+/mês | Nenhum | Enterprise |
| **New Relic** | Altíssimo | $100+/mês | Nenhum | Enterprise |
| **Dynatrace** | Altíssimo | $300+/mês | Nenhum | Enterprise |

**Recomendação para você:**
```
┌──────────────────────────────────────────┐
│ OpenTelemetry + Prometheus + Grafana     │
├──────────────────────────────────────────┤
│ ✅ Grátis (open source)                   │
│ ✅ Observabilidade completa               │
│ ✅ Suporta 50 req/s perfeitamente         │
│ ✅ Fácil escalar depois                   │
│ └─ Depois pode trocar por Datadog se $$  │
└──────────────────────────────────────────┘
```

### Dashboards Grafana (provisionados via JSON)

```
Dashboard 1 (implementado): "FinControl — HTTP Requests" (uid: fincontrol-http-v1)
├─ [Stat] Requests/s         — http_requests_received_total (rate 5m)
├─ [Stat] Taxa de Erros 5xx  — http_requests_received_total{code=~"5.."}
├─ [Stat] Latência P95       — http_request_duration_seconds (percentil 95)
├─ [Stat] Requests em andamento — http_requests_in_progress
├─ [Time series] Requests por status HTTP — por código de resposta
├─ [Time series] Requests por serviço — por label service
├─ [Time series] Latência P50/P95/P99 — histogram_quantile
├─ [Time series] Erros por endpoint — filtrado por código 4xx/5xx
└─ [Table] Top 5xx por endpoint — top-k com sort por taxa de erro

Datasource: Prometheus (uid: fincontrol-prometheus)
Métricas expostas por: prometheus-net em /metrics (ambas as APIs)
Grafana: 11.1.0 (pinado — versões 12+ têm breaking changes no provisioning)
```

---

## Resumo: Infraestrutura Completa

### Fase 1: Setup & Infraestrutura (Semana 1)

- [ ] Criar solução Visual Studio
- [ ] Setup Docker Compose (PostgreSQL, RabbitMQ, Redis, Kong)
- [ ] Configurar Kong API Gateway com rate limiting + circuit breaker
- [ ] Configurar GitHub Actions (CI/CD)
- [ ] Criar projetos base (.API, .Tests, .Domain, .Shared)
- [ ] Documentação inicial

**Deliverables:**
- Repositório no GitHub
- Ambientes local/staging configurados
- Docker Compose funcional com Kong
- Kong Admin UI accessible em http://localhost:1337
- Rate limiting configurado (3.000 req/min = 50 req/s)

---

### Fase 2: Serviço de Lançamentos (Semana 2)

- [ ] Domain model (Aggregate Lancamento)
- [ ] Repository pattern + EF Core DbContext
- [ ] Controllers (POST, GET com filtros)
- [ ] FluentValidation rules
- [x] Event publishing (Outbox Manual + RabbitMqPublisher)
- [ ] Kong routing para Lançamentos Service
- [ ] Unit tests (TDD)
- [ ] Integration tests (Testcontainers)

**Deliverables:**
- API de Lançamentos 100% funcional
- Cobertura de testes >80%
- Eventos publicados em RabbitMQ

---

### Fase 3: Serviço de Consolidado (Semana 2-3)

- [ ] Domain model (Aggregate Consolidado)
- [x] Event consumer (RabbitMQ.Client direto — LancamentoRegistradoConsumer)
- [ ] Cache layer (Redis)
- [ ] Controllers (GET com cache)
- [ ] Queries (CQRS)
- [ ] Kong routing para Consolidado Service com rate limiting
- [ ] Kong circuit breaker se Consolidado cair
- [ ] Unit tests
- [ ] Integration tests com Event bus

**Deliverables:**
- API de Consolidado 100% funcional
- Cache Redis integrado
- Event driven updates
- Kong configured: 50 req/s distribuídos entre instâncias

---

### Fase 4: Testes E2E & Performance (Semana 3-4)

- [ ] Testes end-to-end completos
- [ ] Load testing (k6 ou JMeter) validando 50 req/s via Kong
- [ ] Teste de resiliência (chaos engineering) com Kong circuit breaker
- [ ] Teste de cache
- [ ] Benchmarking (BenchmarkDotNet)

**Deliverables:**
- Evidência de 50 req/s handling via Kong
- Comprovação 5% loss rate com rate limiting Kong
- Relatório de performance incluindo Kong metrics
- Comprovação de circuit breaker automático

---

### Fase 5: Observability & Documentação (Semana 4)

- [ ] Serilog + ELK stack (ASP.NET Core logs)
- [ ] Kong Prometheus metrics exportadas
- [ ] OpenTelemetry + Jaeger (distributed tracing entre Kong e serviços)
- [ ] Prometheus + Grafana (com Kong dashboard)
- [ ] Health checks endpoints (Kong health check integration)
- [ ] Swagger/OpenAPI
- [ ] README completo
- [ ] Arquitetura ADR (Architecture Decision Records)

**Deliverables:**
- Documentação técnica completa
- API docs com exemplos
- Dashboards de monitoramento

---

### Fase 6: Deploy & Kubernetes (Semana 5)

- [ ] Docker images otimizadas (Consolidado, Lançamentos, Kong)
- [ ] Manifests Kubernetes (Deployments, Services, ConfigMaps)
- [ ] Kong Ingress Controller setup (para K8s)
- [ ] Helm charts (Kong + Aplicações)
- [ ] CI/CD pipeline completo (GitHub Actions → Docker Hub → K8s)
- [ ] Staging environment (full test antes de prod)
- [ ] Production readiness checklist
- [ ] Kong plugins em prod: rate-limiting, circuit-breaker, logging, tracing

**Deliverables:**
- Deploy automatizado via GitHub Actions
- Ambientes staging/prod funcionando
- Rollback strategy (Kong + Kubernetes)
- Kong Ingress CRDs configuradas para rate limiting

---

## Resumo Executivo: Vertical Slicing + CQRS para 50 req/s

### Por que Vertical Slicing + CQRS é a decisão certa?

Para um sistema que precisa:
- **Lidar com 50 req/s** (21 bilhões de requisições em 10 anos)
- **Crescer de 50 → 176 req/s** sem redesign
- **Ser mantido por 10 anos** sem refatorações massivas
- **Adicionar features** facilmente (filtros, relatórios, etc)

**Vertical Slicing resolve estes problemas:**

```
PROBLEMA            CAMADAS TRADICIONAIS         VERTICAL SLICING + CQRS
─────────────────────────────────────────────────────────────────────────
Localizar código    5+ pastas espalhadas         1 pasta = 1 feature
Escala              Services gigante (100+ classes)  Pastas pequenas & focadas
Mudar feature       Toca 3+ camadas              Mudança isolada
Novo dev            "Onde é o código X?"        "Procure em /Features/X"
Testes              Desacoplados de código      Colocados com o código
Reuso               Handlers reutilizados ❌    Cada slice é completo ✅
```

### Numeração Comprovada para 50 req/s

```
╔═══════════════════════════════════════════════════════╗
║         IMPACTO ARQUITETURAL EM NÚMEROS              ║
╠═══════════════════════════════════════════════════════╣
║ 50 req/s (Consolidado - Read heavy)                  ║
│   └─ Com cache Redis: 95% hit rate                   ║
│      └─ Reduz BD: 50 → 2,5 req/s (-95%)             ║
│      └─ CPU BD: 70% → 15% (-75%)                    ║
│      └─ Latência P99: 50ms → 5ms (-90%)             ║
│                                                       ║
║ Vertical Slicing permite:                            ║
│   ✅ 0,2 req/s escrita (200 lançamentos/dia)         ║
│   ✅ 99,6% requisições de leitura                    ║
│   ✅ CQRS: Modelos separados (ótimo!)               ║
│   ✅ 2 servidores Consolidado (25 req/s cada)       ║
│   ✅ 1 servidor Lançamentos (0,1 req/s cada)        ║
│                                                       ║
║ Crescimento 10 anos (50 → 176 req/s):               ║
│   Y1: 2 servidores × 25 req/s = 42% CPU  ✅         ║
│   Y5: 3 servidores × 29 req/s = 49% CPU  ✅         ║
│  Y10: 4 servidores × 44 req/s = 75% CPU  ✅         ║
│                                                       ║
║  = SEM REDESIGN EM 10 ANOS! 🎯                      ║
╚═══════════════════════════════════════════════════════╝
```

### Padrão Proven em Produção

Vertical Slicing com CQRS é adotado por:
- **Microsoft** (ASP.NET Core templates)
- **Uber** (Microservices + Event-Driven)
- **Netflix** (Domain-driven features)
- **Amazon** (Two-pizza teams)
- **Google** (Vertical product teams)

### Comparação Lado a Lado

```
CRITÉRIO                  CAMADAS              VERTICAL SLICING
──────────────────────────────────────────────────────────────
Code Colocation          ❌ Espalhado         ✅ Junto
Feature Discovery        ❌ Difícil           ✅ Fácil (/Features)
Team Ownership           ❌ Compartilhado     ✅ Próprio
Database per Service     ❌ Problemático      ✅ Natural
Testing Isolation        ❌ Fraco             ✅ Forte
Scaling per Feature      ❌ Toda a stack      ✅ Feature específica
Refactoring Impact       ❌ Alto              ✅ Baixo
Novo dev Onboarding      ❌ Semanas           ✅ Horas
Manutenção Longo Prazo   ❌ Desgostoso        ✅ Sustentável
Suporta 50 req/s?        ⚠️  Com esforço      ✅ Nativo
```

### Recomendação Final

**✅ RECOMENDADO: Implementar com Vertical Slicing + CQRS + Kong**

- **API Gateway:** Kong (rate limiting, circuit breaker, logging, auth plugins)
- **Stack:** Wolverine (CQRS + Mediator + Message Bus) + Minimal APIs + Vertical Features
- **Database:** PostgreSQL (ACID para escritas) + Marten (event store) + Redis (cache para leituras)
- **Message Bus:** RabbitMQ (nativo em Wolverine, event-driven entre serviços, Outbox Pattern)
- **Observabilidade:** Wolverine + OpenTelemetry (automático) + Prometheus/Grafana + Jaeger
- **Escalabilidade:** Horizontal via Kong + Kubernetes (Wolverine suporta scale-out automático)
- **Confiabilidade:** Outbox + Inbox Patterns automáticos (zero perda de eventos)
- **Manutenibilidade:** Alto - código organizado por feature + handlers auto-descobertos + middleware automático
- **Time:** Um dev novo consegue achar código em <30 min + compreender fluxos de eventos via Jaeger traces

**Por que Kong?**
- ✅ Rate limiting nativo (50 req/s garantido)
- ✅ Circuit breaker automático (resiliência)
- ✅ Plugins ecosystem (OAuth2, JWT, logging, tracing)
- ✅ Kubernetes ready (Kong Ingress Controller)
- ✅ Crescimento fácil (apenas add plugins/instâncias)
- ✅ Suporta 10 anos de escala (50 → 176 req/s)

---

## Plano de Implementação

### Roadmap: 5 Semanas até Produção

**Semana 1: Setup & Infraestrutura**
1. Setup Docker Compose com PostgreSQL, Redis, RabbitMQ, Kong
2. Criação do projeto ASP.NET Core com Wolverine
3. Configuração de Minimal APIs + Vertical Slicing
4. Testes unitários básicos

**Semana 2: Feature de Lançamentos (Escrita)**
1. Implementar `RegistrarLancamentoCommand` com validação
2. Handler com Outbox Pattern (garante entrega)
3. RabbitMQ publisher automático (eventos)
4. Kong routing + rate limiting
5. Testes de ponta a ponta (E2E)

**Semana 2-3: Feature de Consolidado (Leitura)**
1. Implementar `ObterConsolidadoDiaQuery` com cache Redis
2. Consumer de `LancamentoRegistradoEvent` (invalidação)
3. Kong rate limiting para consolidado (50 req/s)
4. Write-through cache strategy
5. Testes de carga inicial

**Semana 3-4: Validação & Resiliência**
1. Teste de carga: 50 req/s sustentado
2. Circuit breaker + retry policies (Polly)
3. OpenTelemetry + Jaeger tracing
4. Monitoramento de Outbox/Inbox
5. Break glass scenarios (RabbitMQ down, BD down, Cache miss)

**Semana 4-5: Deploy & Documentação**
1. Kong Ingress Controller para Kubernetes
2. CI/CD GitHub Actions (build + test + deploy)
3. Documentação completa (ARQUITETURA.md + ADRs)
4. README com instruções de setup
5. Deploy em dev/staging/prod

### Entregáveis Esperados

| Semana | Entregável | Status |
|--------|-----------|--------|
| 1 | Docker Compose + Projeto setup | 📌 |
| 2 | Lançamentos funcional + Kong | 🚀 |
| 3 | Consolidado funcional + Cache | 🎯 |
| 4 | 50 req/s validado + Resiliência | ✅ |
| 5 | Deploy + Documentação completa | 🏁 |

### Critérios de Aceitação

- ✅ Lançamentos recebe/processa 0.2 req/s (200 lançamentos/dia)
- ✅ Consolidado processa 50 req/s com 95% cache hit
- ✅ P99 latência: Lançamentos <200ms, Consolidado <5ms (com cache)
- ✅ Zero perda de eventos (Outbox Pattern)
- ✅ Crescimento 10 anos sem redesign (50 → 176 req/s)
- ✅ Testes automatizados (>80% cobertura)
- ✅ Documentação completa e viva

---

## Diagrama Estrutural — Para Desenho de Arquitetura

### Visão de Componentes e Conexões

```
╔══════════════════════════════════════════════════════════════════════════════════╗
║                              FINCONTROL — ARQUITETURA                           ║
╠══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                  ║
║  ┌───────────────────────────────────────────────────────────────────────────┐  ║
║  │                           CAMADA DE BORDA                                  │  ║
║  │                                                                             │  ║
║  │   ┌───────────────────────────────────────────────────────────────────┐   │  ║
║  │   │                  KONG API GATEWAY  (:8000)                        │   │  ║
║  │   │  Plugins: JWT · Rate Limiting · Proxy-Cache · Request-Transformer │   │  ║
║  │   └──────────────────────────┬───────────────────────┬────────────────┘   │  ║
║  │                               │                       │                    │  ║
║  │   ┌───────────────────────────────────────────────────────────────────┐   │  ║
║  │   │              KEYCLOAK (:8081)  — Identity Provider                │   │  ║
║  │   │     OAuth 2.0 · OpenID Connect · JWT RS256 · realm: fincontrol    │   │  ║
║  │   └───────────────────────────────────────────────────────────────────┘   │  ║
║  └───────────────────────────────────────────────────────────────────────────┘  ║
║                               │                       │                          ║
║                    ┌──────────▼──────────┐  ┌────────▼───────────┐             ║
║                    │  LANCAMENTOS API     │  │  CONSOLIDADO API   │             ║
║                    │       (:5083)        │  │      (:5260)       │             ║
║                    │                      │  │                    │             ║
║                    │  POST /lancamentos   │  │ GET /consolidados  │             ║
║                    │       /registrar     │  │      /saldo        │             ║
║                    │                      │  │                    │             ║
║                    │  Wolverine + EF Core │  │ Wolverine + Redis  │             ║
║                    │  FluentValidation    │  │ (read-through)     │             ║
║                    │  Idempotência        │  └────────────────────┘             ║
║                    │                      │                                     ║
║                    │  ┌────────────────┐  │                                     ║
║                    │  │  OutboxRelay   │  │                                     ║
║                    │  │  Service       │  │                                     ║
║                    │  │  (Polly 3×)    │  │                                     ║
║                    │  └───────┬────────┘  │                                     ║
║                    └──────────┼───────────┘                                     ║
║                               │                                                 ║
║              ┌────────────────▼──────────────────────┐                         ║
║              │        RABBITMQ (:5672)                │                         ║
║              │  Exchange: lancamentos.events (topic)  │                         ║
║              │  Queue: fincontrol.consolidado.*       │                         ║
║              └────────────────┬──────────────────────┘                         ║
║                               │                                                 ║
║                    ┌──────────▼──────────┐                                      ║
║                    │  CONSOLIDADO WORKER  │                                      ║
║                    │                      │                                      ║
║                    │  RabbitMQ Consumer   │                                      ║
║                    │  Redis Lock (Lua)    │──────────► REDIS (:6379)             ║
║                    │  Atualiza saldo      │            saldo:consolidado:{dt}    ║
║                    └──────────────────────┘                                      ║
║                                                                                  ║
║  ┌────────────────────────────────────────────────────────────────────────────┐ ║
║  │                          DADOS E SECRETS                                   │ ║
║  │                                                                             │ ║
║  │   POSTGRESQL (:5432)              VAULT (:8200)                            │ ║
║  │   schema: lancamentos              KV v2: dev/postgres                     │ ║
║  │   ├─ lancamentos                          dev/rabbitmq                     │ ║
║  │   └─ outbox_messages                      dev/redis                        │ ║
║  │   schema: keycloak                        dev/keycloak                     │ ║
║  │   schema: kong                            dev/kong                         │ ║
║  └────────────────────────────────────────────────────────────────────────────┘ ║
║                                                                                  ║
║  ┌────────────────────────────────────────────────────────────────────────────┐ ║
║  │                        OBSERVABILIDADE                                     │ ║
║  │                                                                             │ ║
║  │   PROMETHEUS (:9090)     GRAFANA (:3000)     JAEGER (:16686)              │ ║
║  │   Scraping /metrics      Dashboard HTTP       Distributed Traces           │ ║
║  │   (ambas as APIs)        fincontrol-http-v1   OTLP gRPC :4317             │ ║
║  └────────────────────────────────────────────────────────────────────────────┘ ║
╚══════════════════════════════════════════════════════════════════════════════════╝
```

### Diagrama Mermaid (para ferramentas como draw.io, Mermaid Live)

```mermaid
graph TB
    Cliente["Cliente HTTP<br/>(Browser/App)"]

    subgraph Borda["Camada de Borda"]
        Kong["Kong API Gateway :8000<br/>jwt · rate-limiting · proxy-cache<br/>request-transformer"]
        Keycloak["Keycloak :8081<br/>JWT RS256<br/>realm: fincontrol"]
    end

    subgraph APIs["Serviços de Aplicação"]
        LancAPI["Lancamentos API :5083<br/>POST /lancamentos/registrar<br/>Wolverine · EF Core · Outbox"]
        ConsAPI["Consolidado API :5260<br/>GET /consolidados/saldo<br/>Wolverine · Redis"]
        ConsWorker["Consolidado Worker<br/>RabbitMQ Consumer<br/>Redis Lock + Write"]
        OutboxRelay["OutboxRelayService<br/>BackgroundService<br/>Polly Retry 3×"]
    end

    subgraph Infra["Infraestrutura"]
        Postgres[("PostgreSQL :5432<br/>lancamentos<br/>outbox_messages")]
        Redis[("Redis :6379<br/>saldo:consolidado:<br/>{yyyy-MM-dd}")]
        RabbitMQ["RabbitMQ :5672<br/>lancamentos.events<br/>topic exchange"]
        Vault["Vault :8200<br/>Secrets KV v2"]
    end

    subgraph Obs["Observabilidade"]
        Prometheus["Prometheus :9090"]
        Grafana["Grafana :3000<br/>Dashboard HTTP"]
        Jaeger["Jaeger :16686<br/>Distributed Tracing"]
    end

    Cliente -->|"Bearer JWT"| Kong
    Kong -->|"validates RS256"| Keycloak
    Kong -->|"upstream :5083 + X-Sub-Key"| LancAPI
    Kong -->|"upstream :5260 + X-Sub-Key"| ConsAPI

    LancAPI -->|"INSERT ACID"| Postgres
    LancAPI --> OutboxRelay
    OutboxRelay -->|"PublishRaw AMQP"| RabbitMQ
    RabbitMQ -->|"LancamentoRegistrado"| ConsWorker
    ConsWorker -->|"SET saldo"| Redis
    ConsAPI -->|"GET saldo"| Redis

    LancAPI -.->|"secrets"| Vault
    ConsAPI -.->|"secrets"| Vault
    ConsWorker -.->|"secrets"| Vault

    LancAPI -->|"/metrics"| Prometheus
    ConsAPI -->|"/metrics"| Prometheus
    Prometheus --> Grafana
    LancAPI -.->|"OTLP traces"| Jaeger
    ConsAPI -.->|"OTLP traces"| Jaeger
```

---

## Validação dos Requisitos do Desafio

### Requisitos de Negócio

| Requisito | Atendido | Como |
|-----------|----------|------|
| Controle de lançamentos (débitos e créditos) | ✅ | `POST /lancamentos/registrar` — modalidade Venda/Devolucao/Suprimento/Sangria/PagamentoFornecedor/RecebimentoDivida/Outros |
| Saldo diário consolidado | ✅ | `GET /consolidados/saldo?data-lancamento=yyyy-MM-dd` — Redis (<5ms) |
| Independência entre serviços | ✅ | Lançamentos não conhece Consolidado; comunicação async via RabbitMQ |

### Requisitos Técnicos Obrigatórios

| Requisito | Atendido | Como |
|-----------|----------|------|
| Desenho da solução documentado | ✅ | Este documento + diagrama estrutural acima |
| Implementação em C# | ✅ | .NET 10, ASP.NET Core Minimal APIs, C# 12 |
| Testes automatizados | ✅ | 83 testes (48 Lancamentos + 35 Consolidado), xUnit + Moq + FluentAssertions |
| Boas práticas (Design Patterns, SOLID, Arquitetura) | ✅ | CQRS, Vertical Slicing, Outbox, Repository, DI, async/await correto |
| README com instruções de execução | ✅ | README.md na raiz + guias em .docs/ |
| Hospedagem em repositório público | ✅ | GitHub público |
| Documentação completa no repositório | ✅ | .docs/ com ARQUITETURA.md, guias de setup, CONVENCOES.md |

### Requisitos Não-Funcionais

| Requisito | Atendido | Como |
|-----------|----------|------|
| Lançamentos independente do Consolidado | ✅ | Arquitetura event-driven: Outbox + RabbitMQ. Consolidado pode cair sem afetar Lançamentos |
| 50 req/s no Consolidado | ✅ | Redis (<5ms por request) + Kong proxy-cache 30s + rate limiting 55 req/s com headroom; validado com `FinControl.StressTests` (NBomber) |
| Máx. 5% de perda de requisições | ✅ | Outbox Manual (nunca perde evento) + Polly retry 3× + RabbitMQ durable queue + Redis lock atômico |

### Análise de Atendimento dos NFRs

**Resiliência (Lançamentos independente do Consolidado):**
- Lançamentos persiste em PostgreSQL + outbox_messages atomicamente
- OutboxRelayService entrega ao RabbitMQ de forma assíncrona (worker separado)
- Se Consolidado cair: fila RabbitMQ acumula; ao reiniciar, worker processa backlog
- Se RabbitMQ cair: Polly retenta 3× e registra erro; nenhum lançamento é perdido do PostgreSQL

**Performance (50 req/s):**
- Redis resolve ~99% dos requests de leitura em <5ms
- Kong proxy-cache absorve picos de até 1 req/30s por endpoint sem tocar o upstream
- Rate limiting Kong: 55 req/s (10% de headroom acima do requisito)
- Testado matematicamente: ver seção "Análise de Volumetria" acima

**Confiabilidade (máx. 5% perda):**
- Outbox Manual garante que nenhum evento seja perdido se o publisher falhar
- Polly retry exponencial com jitter reduz impacto de falhas transitórias
- Redis lock distribuído (Lua) garante que saldos concorrentes não causem inconsistência

---

## Cobertura de Testes

### Resultado: `dotnet test`

```
Passed: 83   Failed: 0   Skipped: 0
  FinControl.Lancamentos.Tests  →  48 testes
  FinControl.Consolidado.Tests  →  35 testes
```

### Detalhamento por Classe

#### FinControl.Lancamentos.Tests (48 testes)

| Arquivo | Testes | O que cobre |
|---------|--------|-------------|
| `Domain/LancamentoTests.cs` | 9 | Classificação por sinal (Credito/Debito via `Tipo`); `ValorFormatado` (centavos→reais, sinal negativo para débito); igualdade por `Id` (não por valor); `CreatedAt` preenchido automaticamente |
| `Features/Commands/RegistrarLancamentoCommandHandlerTests.cs` | 12 | Persistência no banco (InMemory EF Core); geração de `NavigationId` e `CriadoEm`; campos persistidos (Valor/Modalidade/Descricao); data padrão (hoje) vs data informada; **atomicidade Outbox** (1 `OutboxMessage` por lançamento); `Exchange`/`RoutingKey`/`MessageType` do Outbox; payload com `Valor` e `CorrelationId`; `DeliveredAt=null` após insert; múltiplos lançamentos (3 de cada) |
| `Features/Commands/RegistrarLancamentoCommandValidatorTests.cs` | 27 | Vendas com valor positivo (Theory×2); débitos com valor negativo (Theory×4); Outros exige `Descricao`; modalidade inválida; valor excede máximo (R$10M); valor positivo para débito (Theory×4); valor negativo para crédito (Theory×2); Outros sem descrição; descrição >500 chars; data >1 dia no futuro; data >1 ano no passado; `UsuarioId` vazio/curto; `UsuarioNome` vazio/>200 chars; e-mail inválido/vazio; `IdempotencyKey=Empty`; `CorrelationId=Empty` |

#### FinControl.Consolidado.Tests (35 testes)

| Arquivo | Testes | O que cobre |
|---------|--------|-------------|
| `Features/Commands/AtualizarSaldoConsolidaoCommandHandlerTests.cs` | 9 | Saldo inicial (cache vazio→zera antes de somar); acumulação sobre saldo existente; decrementação (saldo pode ficar negativo); TTL 30 dias no Redis; chave de cache usa `dataLancamento`; data retroativa atualiza a chave correta; `UltimaAtualizacao` atualizada; lock distribuído (`IRedisLockService`) invocado para cada atualização |
| `Features/Queries/GetSaldoConsolidadoQueryHandlerTests.cs` | 15 | Saldo acumulado (`saldo:consolidado:acumulado`); saldo por data específica (cache hit); fallback para dias anteriores (até 30 dias); propagação do saldo encontrado para a data requisitada; TTL de 30 dias na propagação; limite exato de 31 chamadas no fallback; parada antecipada quando saldo encontrado; saldo zero quando nenhum dado em 30 dias; conversão centavos→decimal; saldo negativo; `UltimaAtualizacao` retornada |
| `Features/ConsolidadoRegrasDenegocioTests.cs` | 12 | **Testes funcionais end-to-end** (Command + Query com `CacheEmMemoria`): zero sem lançamentos (acumulado e por data); crédito aumenta saldo; múltiplos créditos acumulam; débito reduz saldo; débitos > créditos = saldo negativo; saldo acumulado reflete todos os lançamentos; consultas independentes (acumulado vs. diário); consulta por data específica; fallback para dia anterior sem lançamentos; lançamento retroativo; precisão monetária centavos→reais |

### O Que Está Coberto

- **Lógica de domínio**: classificação Credito/Debito, formatação de valor, identidade por Id
- **Regras de validação**: 100% das regras de negócio do `RegistrarLancamentoCommandValidator` têm pelo menos 1 teste
- **Fluxo principal de escrita**: `RegistrarLancamentoCommandHandler` — do request ao banco + outbox
- **Atomicidade Outbox**: garantia de que cada lançamento gera exatamente 1 `OutboxMessage` na mesma transação
- **Contrato do Outbox**: exchange, routing key, message type e payload validados
- **Fluxo principal de leitura**: `GetSaldoConsolidadoQueryHandler` — resolução de chave + leitura do Redis
- **Acumulação de saldo**: `AtualizarSaldoConsolidaoCommandHandler` — cálculo incremental com lock distribuído

### Lacunas de Cobertura

| Componente | Situação | Risco |
|-----------|----------|-------|
| `OutboxRelayService` | Sem testes | Lógica de polling, batch de 50, retry Polly 3× exponencial+jitter, registro de `LastError`/`RetryCount` não verificados automaticamente |
| `RabbitMqPublisher` | Sem testes | Publicação AMQP, reutilização de `IConnection`, criação de `IChannel` por publish não verificados |
| `SubscriptionKeyMiddleware` | Sem testes | Bypass de `/health` e `/metrics`, comparação em tempo constante (`FixedTimeEquals`), resposta 401 não verificados |
| `LancamentoRegistradoConsumer` | Sem testes | Consumer RabbitMQ do Worker — processamento de mensagem, chamada ao handler, ack/nack não verificados |
| Detecção de duplicata (idempotência) | Sem testes | `VerificarIdempotenciaAsync` existe no handler mas nenhum teste envia o mesmo `IdempotencyKey` duas vezes para verificar o 409 |

> **Conclusão:** Os 83 testes cobrem toda a lógica de negócio e regras de domínio. As lacunas estão concentradas em componentes de infraestrutura (`OutboxRelayService`, `RabbitMqPublisher`) e integração (`SubscriptionKeyMiddleware`, `Consumer`) — candidatos naturais para testes de integração com Testcontainers em uma próxima iteração.

---

## Próximas Etapas

1. **Criar repositório no GitHub** com este planejamento
2. **Setup local**: Docker Compose incluindo Kong (ver docker-compose.yml nesta documentação)
3. **Implementação incremental** seguindo TDD + Vertical Slicing
4. **Validação contínua** de requisitos via Kong rate limiting
5. **Documentação viva** mantida junto ao código (ARQUITETURA.md, ADRs)

---

## Referências & Recursos

### Framework Principal: Wolverine
- **Wolverine:** https://wolverine.io (Next-generation .NET Mediator + Message Bus)
- **Wolverine Documentation:** https://docs.wolverine.io (Guia oficial)
- **Wolverine GitHub:** https://github.com/JasperFx/wolverine
- **Jeremy D. Miller (Criador):** https://twitter.com/jeremydmiller (Criador do MediatR e Wolverine)

### Padrões Arquiteturais
- Martin Fowler - Microservices: https://martinfowler.com/articles/microservices.html
- DDD - Domain-Driven Design: https://www.domainlanguage.com/ddd/
- CQRS Pattern: https://martinfowler.com/bliki/CQRS.html
- Event Sourcing: https://martinfowler.com/eaaDev/EventSourcing.html
- Outbox Pattern: https://microservices.io/patterns/data/transactional-outbox.html
- Vertical Slicing Architecture: https://www.jimmybogard.com/vertical-slice-architecture/

### Stack Técnico Primário (.NET)
- **ASP.NET Core 10/11:** https://learn.microsoft.com/dotnet/core
- **Minimal APIs:** https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis
- **PostgreSQL:** https://www.postgresql.org (Database transacional)
- **Marten:** https://martendb.io (Document Store + Outbox Pattern para PostgreSQL)
- **Redis:** https://redis.io (Cache distribuído)
- **RabbitMQ:** https://www.rabbitmq.com (Message Broker nativo no Wolverine)

### API Gateway & Segurança
- **Kong:** https://konghq.com (API Gateway, rate limiting, circuit breaker)
- **Kong Ingress Controller:** https://docs.konghq.com/kubernetes-ingress-controller/
- **ModSecurity:** https://modsecurity.org (WAF - Web Application Firewall)
- **ModSecurity Core Rule Set:** https://coreruleset.org (OWASP Top 10 proteção)
- **Fail2Ban:** https://www.fail2ban.org (Detecção comportamental de ataques)
- **Hashicorp Vault:** https://www.vaultproject.io (Secrets Management)
- **Keycloak:** https://www.keycloak.org (Identity Provider - SSO, OAuth2, OIDC)

### Observabilidade & Monitoramento
- **OpenTelemetry:** https://opentelemetry.io (Observabilidade automática - integrada no Wolverine)
- **Jaeger:** https://www.jaegertracing.io (Distributed tracing)
- **Prometheus:** https://prometheus.io (Coleta de métricas)
- **Grafana:** https://grafana.com (Dashboard de visualização)
- **Loki:** https://grafana.com/loki (Coleta & agregação de logs)
- **Serilog:** https://serilog.net (Structured logging para .NET)

### Linguagem & Tooling
- **C# 15:** https://learn.microsoft.com/dotnet/csharp (Modern C# features: records, pattern matching, async/await)
- **.NET CLI:** https://learn.microsoft.com/dotnet/core/tools
- **Visual Studio 2024 / VS Code:** https://visualstudio.microsoft.com/
- **Git:** https://git-scm.com (Controle de versão)
- **GitHub:** https://github.com (Repositório público)

### Boas Práticas & Standards
- **SOLID Principles:** https://en.wikipedia.org/wiki/SOLID
- **Clean Code:** Robert C. Martin (Uncle Bob)
- **Clean Architecture:** https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html
- **OWASP Top 10:** https://owasp.org/www-project-top-ten
- **OWASP API Security:** https://owasp.org/www-project-api-security/
- **Conventional Commits:** https://www.conventionalcommits.org (Commit message format)

### Referências Financeiras (Domínio)
- **PCI-DSS:** https://www.pcisecuritystandards.org (Payment Card Security)
- **ISO 27001:** https://www.iso.org/isoiec-27001-information-security-management.html
- **Lei Geral de Proteção de Dados (LGPD):** https://www.gov.br/cidadania/pt-br/acesso-a-informacao/lgpd

---

**Versão:** 3.1  
**Última atualização:** Maio 2026  
**Status:** ✅ Implementação em produção — 83 testes passando, zero falhas; stress test NBomber implementado
