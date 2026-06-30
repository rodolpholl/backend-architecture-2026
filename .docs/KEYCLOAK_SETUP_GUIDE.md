# Keycloak Setup - FinControl

## Objetivo

Configurar Keycloak com realm **fincontrol** e cliente **fincontrol-backend** para emissão de tokens JWT RS256 validados pelo Kong e pelas APIs .NET.

> **Integração com Kong:** O Kong usa o plugin `jwt` nativo com a chave pública RS256 do Keycloak — não há plugin OIDC. Ver [KONG_KEYCLOAK_OIDC.md](KONG_KEYCLOAK_OIDC.md).

> **Nota:** A configuracao abaixo e feita **automaticamente** pelo container `keycloak-init` ao executar `docker-compose up -d`. Este guia serve para configuracao manual ou para recriar o ambiente em caso de falha do init.

---

## Acesso ao Keycloak Admin Console

```
URL:      http://localhost:8081
Username: admin
Password: fincontrol_keycloak_password_123
```

---

## Passo 1: Criar Realm "fincontrol"

### 1.1 - Menu de Realms
```
[Canto superior esquerdo]
  v "Master" (dropdown)
  |- Create Realm
```

### 1.2 - Dados do Novo Realm
```
Realm name:   fincontrol
Display name: FinControl Backend
```

Clique **Create**.

---

## Passo 2: Criar Cliente fincontrol-backend

```
Menu esquerdo: Clients > Create client
```

| Campo | Valor |
|-------|-------|
| **Client ID** | `fincontrol-backend` |
| **Client Protocol** | `openid-connect` |
| Client authentication | habilitado |
| Direct access grants | habilitado (para password flow de desenvolvimento) |

Clique **Save**. Na aba **Credentials**, defina o secret como `fincontrol-backend-secret-12345`.

O `keycloak-init` salva o secret no Vault em `secret/dev/keycloak → api_client_secret`.

---

## Passo 4: Criar Roles

```
Menu esquerdo: Realm roles > Create role
```

| Role | Descricao |
|------|-----------|
| `api-user` | Acesso de leitura/escrita nas APIs |
| `admin` | Acesso administrativo |

---

## Passo 5: Criar Usuarios de Teste

### 5.1 - Criar usuarios

Criar os seguintes usuários via **Users > Add user**:

| Username | Email | First Name | Last Name | Password | Role |
|----------|-------|------------|-----------|----------|------|
| `admin.fincontrol` | `admin@fincontrol.local` | Admin | FinControl | `Admin@123456` | `fincontrol-admin` |
| `user.fincontrol` | `user@fincontrol.local` | User | FinControl | `User@123456` | `fincontrol-user` |
| `contador.fincontrol` | `contador@fincontrol.local` | Contador | FinControl | `Contador@123456` | `fincontrol-accountant` |

Para cada usuário:
1. Criar em **Users > Add user** com `Email verified: [x]` e `Enabled: [x]`
2. Definir senha em **Credentials > Set password** com `Temporary: [ ]` (desmarcado)
3. Atribuir role em **Role mapping > Assign role**

---

## Passo 6: Validar Configuracao

### OIDC Discovery

```bash
curl -s http://localhost:8081/realms/fincontrol/.well-known/openid-configuration | jq '{issuer, token_endpoint, jwks_uri}'
```

Resultado esperado:
```json
{
  "issuer": "http://localhost:8081/realms/fincontrol",
  "token_endpoint": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token",
  "jwks_uri": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs"
}
```

### Obter Token (client_credentials)

```bash
curl -s -X POST http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=kong-client&client_secret=kong-secret&grant_type=client_credentials" | jq .access_token
```

### Obter Token (password flow — usuario de teste)

```bash
curl -s -X POST http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend&client_secret=fincontrol-backend-secret-12345&grant_type=password&username=admin.fincontrol&password=Admin@123456" | jq .access_token
```

---

## Checklist de Configuracao

- [ ] Realm "fincontrol" criado
- [ ] Cliente "fincontrol-backend" criado com client authentication + direct access grants
- [ ] Client secret definido como `fincontrol-backend-secret-12345` e salvo no Vault (`secret/dev/keycloak → api_client_secret`)
- [ ] Roles `fincontrol-admin`, `fincontrol-user` e `fincontrol-accountant` criadas
- [ ] Usuários `admin.fincontrol`, `user.fincontrol` e `contador.fincontrol` criados com senhas permanentes
- [ ] JWKS retornando chave RS256 (`/realms/fincontrol/protocol/openid-connect/certs`)
- [ ] Token JWT obtido com sucesso e validado pelo Kong

---

## Troubleshooting

| Problema | Solucao |
|----------|---------|
| `Invalid client credentials` | Verificar client_secret no Vault: `secret/dev/keycloak → api_client_secret` |
| Realm nao encontrado | Verificar se e `fincontrol` (nao `master` nem `agile`) |
| `connection refused` em :8081 | Keycloak ainda inicializando — aguardar healthcheck |
| Kong nao valida tokens | Issuer no Kong deve ser `http://localhost:8081/realms/fincontrol` |
| Container keycloak-init falhou | `docker-compose logs keycloak-init` para diagnostico |

---

**Versao:** 3.0
**Ultima atualizacao:** Maio 2026
**Status:** Ativo (configuracao automatizada via keycloak-init)
