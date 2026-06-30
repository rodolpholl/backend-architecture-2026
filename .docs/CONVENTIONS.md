# Documentation Conventions

> **Fundamental Premise:** All project documentation **ALWAYS** goes in the `.docs/` folder, never scattered throughout the repository root.

---

## Principles

### 1. Single Centralization
```
✅ CORRECT:     .docs/DOCUMENT_NAME.md
❌ INCORRECT:   ./DOCUMENT_NAME.md
❌ INCORRECT:   ./docs/DOCUMENT_NAME.md
❌ INCORRECT:   ./src/DOCUMENT_NAME.md
```

### 2. Documentation is Code
- Versioned together with the code
- Reviewed together with PRs
- Updated when the architecture changes

### 3. Single Source of Truth (SSOT)
- Do not duplicate information between files
- Use cross-references when necessary
- `ARCHITECTURE.md` is the central and authoritative document

---

## Document Categories

### Architecture & Design
Architectural decisions, patterns, technological justifications.

**Examples:**
- `ARCHITECTURE.md` — Main decisions, current implementation
- `ADR-001-CQRS.md` — Architecture Decision Records

**Prefix:** `ARCHITECTURE_`, `PATTERNS_`, `ADR-`

### Operations & Infrastructure
Operational guides for running and configuring the system.

**Examples:**
- `DOCKER-COMPOSE-EXECUTION-GUIDE.md` — How to bring up local infrastructure
- `VAULT-INITIALIZATION.md` — How Vault is initialized
- `KEYCLOAK_SETUP_GUIDE.md` — How to configure Keycloak
- `KONG_KEYCLOAK_OIDC.md` — Kong + Keycloak OIDC integration
- `INIT-CONTAINERS-CLEANUP.md` — Initialization and cleanup sequence

**Prefix:** `DOCKER_`, `VAULT_`, `KONG_`, `KEYCLOAK_`, `INIT_`

### Security & Compliance
Security policies, compliance, secrets management.

**Prefix:** `SECURITY_`, `COMPLIANCE_`, `SECRETS_`

---

## Standard Template

```markdown
# Descriptive Title

> **Objective:** A clear statement of what this document does.
> **Audience:** Who should read this
> **Last updated:** Month Year

---

## Section 1

Content.

---

**Version:** 1.0
**Status:** Active
```

---

## Checklist: Before Committing

- [ ] **Location:** File in `.docs/`?
- [ ] **Naming:** Follows convention (SCREAMING_SNAKE_CASE.md)?
- [ ] **Content:**
  - [ ] Clear objective
  - [ ] Practical examples
  - [ ] Correct internal links (all referenced files exist)
  - [ ] Update date
- [ ] **Quality:**
  - [ ] No typos/errors
  - [ ] Consistent Markdown formatting
  - [ ] Code would compile/run if necessary
  - [ ] Credentials and names match `docker-compose.yml`

---

## Do Not

```
❌ Do not create documentation in the repository root
❌ Do not duplicate content in multiple files
❌ Do not use outdated documentation as reference
❌ Do not use credentials or container names that differ from docker-compose.yml
✅ Keep everything in .docs/
✅ Use cross-references
✅ Verify credentials against docker-compose.yml before committing
```

---

## Cross-References

**Within `.docs/`:**
```markdown
See [ARCHITECTURE.md](ARCHITECTURE.md) for more details.
```

**Outside of `.docs/`:**
```markdown
See [docker-compose.yml](../docker-compose.yml) for service configuration.
```

---

## Current Structure of `.docs/`

```
.docs/
├── ARCHITECTURE.md                    ← Central and authoritative document (v3.0)
├── CONVENTIONS.md                     ← This file
├── PROGRESS.md                        ← Implementation status and roadmap
├── DOCKER-COMPOSE-EXECUTION-GUIDE.md ← How to bring up local infrastructure
├── INIT-CONTAINERS-CLEANUP.md        ← Boot sequence and init containers cleanup
├── KEYCLOAK_SETUP_GUIDE.md           ← Keycloak configuration (realm, clients, users)
├── KONG_KEYCLOAK_OIDC.md             ← Kong + JWT RS256 integration (Keycloak)
├── KONG_KEYCLOAK_TESTS.md            ← Kong/Keycloak integration tests
├── REFRESH_TOKEN_FLOW.md             ← JWT token lifecycle and refresh flow in client
├── VAULT-INITIALIZATION.md           ← Automatic Vault initialization (secrets)
└── desafio-arquiteto-software.pdf    ← Original challenge specification
```

---

**Fundamental Premise:** Centralized documentation = maintained documentation = useful documentation.

---

**Version:** 1.2
**Status:** Active
**Last updated:** May 2026
