# Keycloak Setup - FinControl

## Objective

Configure Keycloak with realm **fincontrol** and client **fincontrol-backend** to issue JWT RS256 tokens validated by Kong and .NET APIs.

> **Kong Integration:** Kong uses the native `jwt` plugin with Keycloak's RS256 public key — there is no OIDC plugin. See [KONG_KEYCLOAK_OIDC.md](KONG_KEYCLOAK_OIDC.md).

> **Note:** The configuration below is done **automatically** by the `keycloak-init` container when running `docker-compose up -d`. This guide is for manual configuration or recreating the environment in case of init failure.

---

## Access Keycloak Admin Console

```
URL:      http://localhost:8081
Username: admin
Password: fincontrol_keycloak_password_123
```

---

## Step 1: Create "fincontrol" Realm

### 1.1 - Realms Menu
```
[Top left corner]
  v "Master" (dropdown)
  |- Create Realm
```

### 1.2 - New Realm Data
```
Realm name:   fincontrol
Display name: FinControl Backend
```

Click **Create**.

---

## Step 2: Create fincontrol-backend Client

```
Left menu: Clients > Create client
```

| Field | Value |
|-------|-------|
| **Client ID** | `fincontrol-backend` |
| **Client Protocol** | `openid-connect` |
| Client authentication | enabled |
| Direct access grants | enabled (for development password flow) |

Click **Save**. In the **Credentials** tab, set the secret to `fincontrol-backend-secret-12345`.

The `keycloak-init` saves the secret in Vault at `secret/dev/keycloak → api_client_secret`.

---

## Step 4: Create Roles

```
Left menu: Realm roles > Create role
```

| Role | Description |
|------|-----------|
| `api-user` | Read/write access to APIs |
| `admin` | Administrative access |

---

## Step 5: Create Test Users

### 5.1 - Create users

Create the following users via **Users > Add user**:

| Username | Email | First Name | Last Name | Password | Role |
|----------|-------|------------|-----------|----------|------|
| `admin.fincontrol` | `admin@fincontrol.local` | Admin | FinControl | `Admin@123456` | `fincontrol-admin` |
| `user.fincontrol` | `user@fincontrol.local` | User | FinControl | `User@123456` | `fincontrol-user` |
| `contador.fincontrol` | `contador@fincontrol.local` | Contador | FinControl | `Contador@123456` | `fincontrol-accountant` |

For each user:
1. Create in **Users > Add user** with `Email verified: [x]` and `Enabled: [x]`
2. Set password in **Credentials > Set password** with `Temporary: [ ]` (unchecked)
3. Assign role in **Role mapping > Assign role**

---

## Step 6: Validate Configuration

### OIDC Discovery

```bash
curl -s http://localhost:8081/realms/fincontrol/.well-known/openid-configuration | jq '{issuer, token_endpoint, jwks_uri}'
```

Expected result:
```json
{
  "issuer": "http://localhost:8081/realms/fincontrol",
  "token_endpoint": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/token",
  "jwks_uri": "http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs"
}
```

### Get Token (client_credentials)

```bash
curl -s -X POST http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=kong-client&client_secret=kong-secret&grant_type=client_credentials" | jq .access_token
```

### Get Token (password flow — test user)

```bash
curl -s -X POST http://localhost:8081/realms/fincontrol/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=fincontrol-backend&client_secret=fincontrol-backend-secret-12345&grant_type=password&username=admin.fincontrol&password=Admin@123456" | jq .access_token
```

---

## Configuration Checklist

- [ ] Realm "fincontrol" created
- [ ] Client "fincontrol-backend" created with client authentication + direct access grants
- [ ] Client secret set to `fincontrol-backend-secret-12345` and saved in Vault (`secret/dev/keycloak → api_client_secret`)
- [ ] Roles `fincontrol-admin`, `fincontrol-user` and `fincontrol-accountant` created
- [ ] Users `admin.fincontrol`, `user.fincontrol` and `contador.fincontrol` created with permanent passwords
- [ ] JWKS returning RS256 key (`/realms/fincontrol/protocol/openid-connect/certs`)
- [ ] JWT token obtained successfully and validated by Kong

---

## Troubleshooting

| Issue | Solution |
|----------|---------|
| `Invalid client credentials` | Check client_secret in Vault: `secret/dev/keycloak → api_client_secret` |
| Realm not found | Verify it is `fincontrol` (not `master` or `agile`) |
| `connection refused` on :8081 | Keycloak still initializing — wait for healthcheck |
| Kong does not validate tokens | Issuer in Kong must be `http://localhost:8081/realms/fincontrol` |
| Container keycloak-init failed | `docker-compose logs keycloak-init` for diagnostics |

---

**Version:** 3.0
**Last updated:** May 2026
**Status:** Active (automated configuration via keycloak-init)
