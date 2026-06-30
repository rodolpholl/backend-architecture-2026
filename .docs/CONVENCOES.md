# Convenções de Documentação

> **Premissa Fundamental:** Toda documentação do projeto **SEMPRE** fica na pasta `.docs/`, nunca espalhada pela raiz do repositório.

---

## Princípios

### 1. Centralização Única
```
✅ CORRETO:     .docs/NOME_DOCUMENTO.md
❌ INCORRETO:   ./NOME_DOCUMENTO.md
❌ INCORRETO:   ./docs/NOME_DOCUMENTO.md
❌ INCORRETO:   ./src/NOME_DOCUMENTO.md
```

### 2. Documentação é Código
- Versionada junto com o código
- Revisada junto com PRs
- Atualizada quando a arquitetura muda

### 3. Single Source of Truth (SSOT)
- Não duplicar informações entre arquivos
- Usar referências cruzadas quando necessário
- `ARQUITETURA.md` é o documento central e autoritativo

---

## Categorias de Documentos

### Arquitetura & Design
Decisões arquiteturais, padrões, justificativas tecnológicas.

**Exemplos:**
- `ARQUITETURA.md` — Decisões principais, implementação atual
- `ADR-001-CQRS.md` — Architecture Decision Records

**Prefixo:** `ARQUITETURA_`, `PADROES_`, `ADR-`

### Operações & Infraestrutura
Guias operacionais para executar e configurar o sistema.

**Exemplos:**
- `DOCKER-COMPOSE-EXECUTION-GUIDE.md` — Como subir a infraestrutura local
- `VAULT-INITIALIZATION.md` — Como o Vault é inicializado
- `KEYCLOAK_SETUP_GUIDE.md` — Como configurar o Keycloak
- `KONG_KEYCLOAK_OIDC.md` — Integração Kong + Keycloak OIDC
- `INIT-CONTAINERS-CLEANUP.md` — Sequência de inicialização e limpeza

**Prefixo:** `DOCKER_`, `VAULT_`, `KONG_`, `KEYCLOAK_`, `INIT_`

### Segurança & Compliance
Políticas de segurança, compliance, secrets management.

**Prefixo:** `SEGURANCA_`, `COMPLIANCE_`, `SECRETS_`

---

## Template Padrão

```markdown
# Título Descritivo

> **Objetivo:** Uma frase clara do que este documento faz.
> **Audiência:** Quem deve ler
> **Última atualização:** Mês Ano

---

## Seção 1

Conteúdo.

---

**Versão:** 1.0
**Status:** Ativo
```

---

## Checklist: Antes de Commitar

- [ ] **Localização:** Arquivo em `.docs/`?
- [ ] **Nomenclatura:** Segue convenção (SCREAMING_SNAKE_CASE.md)?
- [ ] **Conteúdo:**
  - [ ] Objetivo claro
  - [ ] Exemplos práticos
  - [ ] Links internos corretos (todos os arquivos referenciados existem)
  - [ ] Data de atualização
- [ ] **Qualidade:**
  - [ ] Sem typos/erros
  - [ ] Formatação Markdown consistente
  - [ ] Código compilaria/rodaria se necessário
  - [ ] Credenciais e nomes batem com `docker-compose.yml`

---

## Não Faça

```
❌ Não crie documentação na raiz do repositório
❌ Não duplique conteúdo em múltiplos arquivos
❌ Não use documentação desatualizada como referência
❌ Não use credenciais ou nomes de containers que diferem do docker-compose.yml
✅ Mantenha tudo em .docs/
✅ Use referências cruzadas
✅ Verifique credenciais contra o docker-compose.yml antes de commitar
```

---

## Referências Cruzadas

**Dentro de `.docs/`:**
```markdown
Veja [ARQUITETURA.md](ARQUITETURA.md) para mais detalhes.
```

**Para fora de `.docs/`:**
```markdown
Veja [docker-compose.yml](../docker-compose.yml) para configuração dos serviços.
```

---

## Estrutura Atual de `.docs/`

```
.docs/
├── ARQUITETURA.md                    ← Documento central e autoritativo (v3.0)
├── CONVENCOES.md                     ← Este arquivo
├── PROGRESSO.md                      ← Status de implementação e roadmap
├── DOCKER-COMPOSE-EXECUTION-GUIDE.md ← Como subir a infra local
├── INIT-CONTAINERS-CLEANUP.md        ← Sequência de boot e limpeza dos init containers
├── KEYCLOAK_SETUP_GUIDE.md           ← Configuração do Keycloak (realm, clientes, usuários)
├── KONG_KEYCLOAK_OIDC.md             ← Integração Kong + JWT RS256 (Keycloak)
├── KONG_KEYCLOAK_TESTS.md            ← Testes de integração Kong/Keycloak
├── REFRESH_TOKEN_FLOW.md             ← Ciclo de vida dos tokens JWT e fluxo de refresh no cliente
├── VAULT-INITIALIZATION.md           ← Inicialização automática do Vault (secrets)
└── desafio-arquiteto-software.pdf    ← Especificação original do desafio
```

---

**Premissa Fundamental:** Documentação centralizada = documentação mantida = documentação útil.

---

**Versão:** 1.2
**Status:** Ativo
**Última atualização:** Maio 2026
