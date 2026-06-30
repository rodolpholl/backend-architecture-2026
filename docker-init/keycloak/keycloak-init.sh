#!/bin/bash

################################################################################
# Inicializa o Keycloak com Realm, Clients, Roles e UsuÃ¡rios para FinControl
#
# VariÃ¡veis de ambiente:
#   KEYCLOAK_URL      - URL do Keycloak (padrÃ£o: http://keycloak:8080)
#   KEYCLOAK_USER     - UsuÃ¡rio admin (padrÃ£o: admin)
#   KEYCLOAK_PASSWORD - Senha admin (padrÃ£o: fincontrol_keycloak_password_123)
#   REALM_NAME        - Nome do realm (padrÃ£o: fincontrol)
################################################################################

set -euo pipefail

KEYCLOAK_URL="${KEYCLOAK_URL:-http://keycloak:8080}"
KEYCLOAK_USER="${KEYCLOAK_USER:-admin}"
KEYCLOAK_PASSWORD="${KEYCLOAK_PASSWORD:-fincontrol_keycloak_password_123}"
REALM_NAME="${REALM_NAME:-fincontrol}"

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

print_header() {
    echo -e "\n${CYAN}â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•${NC}\n"
}
print_info()    { echo -e "${CYAN}â„¹${NC}  $1"; }
print_success() { echo -e "${GREEN}âœ“${NC}  $1"; }
print_error()   { echo -e "${RED}âœ—${NC}  $1"; }
print_warning() { echo -e "${YELLOW}âš ${NC}  $1"; }

################################################################################
# Aguarda Keycloak ficar totalmente pronto (endpoint /realms/master acessÃ­vel)
################################################################################

wait_for_keycloak() {
    print_info "Aguardando Keycloak ficar pronto em ${KEYCLOAK_URL}..."
    local max_attempts=60
    local attempt=0

    until curl -s -f -o /dev/null "${KEYCLOAK_URL}/realms/master"; do
        attempt=$((attempt + 1))
        if [ $attempt -ge $max_attempts ]; then
            print_error "Keycloak nÃ£o ficou pronto apÃ³s ${max_attempts} tentativas."
            exit 1
        fi
        echo "  Tentativa ${attempt}/${max_attempts} â€” aguardando..."
        sleep 3
    done

    print_success "Keycloak estÃ¡ pronto!"
}

################################################################################
# ObtÃ©m token de acesso do admin via password grant
################################################################################

get_admin_token() {
    print_info "Obtendo token de acesso do admin..."

    local response
    response=$(curl -s -X POST \
        "${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "grant_type=password" \
        -d "client_id=admin-cli" \
        -d "username=${KEYCLOAK_USER}" \
        -d "password=${KEYCLOAK_PASSWORD}")

    ADMIN_TOKEN=$(echo "$response" | jq -r '.access_token')

    if [ -z "$ADMIN_TOKEN" ] || [ "$ADMIN_TOKEN" = "null" ]; then
        print_error "Falha ao obter token de admin. Resposta: $response"
        exit 1
    fi

    print_success "Token de admin obtido com sucesso!"
}

################################################################################
# Cria o Realm fincontrol se nÃ£o existir
################################################################################

create_realm() {
    print_header "Criando Realm: ${REALM_NAME}"

    local status
    status=$(curl -s -o /dev/null -w "%{http_code}" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}")

    if [ "$status" = "200" ]; then
        print_warning "Realm '${REALM_NAME}' jÃ¡ existe. Pulando criaÃ§Ã£o."
        return 0
    fi

    curl -s -X POST "${KEYCLOAK_URL}/admin/realms" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        -H "Content-Type: application/json" \
        -d "{
            \"realm\": \"${REALM_NAME}\",
            \"displayName\": \"FinControl\",
            \"displayNameHtml\": \"<b>FinControl</b>\",
            \"enabled\": true,
            \"sslRequired\": \"external\",
            \"registrationAllowed\": false,
            \"loginWithEmailAllowed\": true,
            \"duplicateEmailsAllowed\": false,
            \"resetPasswordAllowed\": true,
            \"editUsernameAllowed\": false,
            \"bruteForceProtected\": true,
            \"accessTokenLifespan\": 300,
            \"refreshTokenMaxReuse\": 0,
            \"ssoSessionIdleTimeout\": 1800,
            \"ssoSessionMaxLifespan\": 36000,
            \"internationalizationEnabled\": true,
            \"supportedLocales\": [\"pt-BR\", \"en\"],
            \"defaultLocale\": \"pt-BR\"
        }" > /dev/null

    print_success "Realm '${REALM_NAME}' criado com sucesso!"
}

################################################################################
# Cria um client no realm
################################################################################

create_client() {
    local client_id="$1"
    local client_payload="$2"

    # Verifica se jÃ¡ existe
    local existing
    existing=$(curl -s \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients?clientId=${client_id}" | jq length)

    if [ "$existing" -gt "0" ]; then
        print_warning "Client '${client_id}' jÃ¡ existe. Pulando."
        return 0
    fi

    curl -s -X POST "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        -H "Content-Type: application/json" \
        -d "$client_payload" > /dev/null

    print_success "Client '${client_id}' criado!"
}

################################################################################
# Cria Clients para Backend, Frontend e Kong
################################################################################

create_clients() {
    print_header "Criando Clients"

    # Backend confidential client (microserviÃ§os .NET)
    create_client "fincontrol-backend" '{
        "clientId": "fincontrol-backend",
        "name": "FinControl Backend",
        "description": "Client para microserviÃ§os .NET do FinControl",
        "enabled": true,
        "clientAuthenticatorType": "client-secret",
        "secret": "fincontrol-backend-secret-12345",
        "standardFlowEnabled": false,
        "directAccessGrantsEnabled": true,
        "serviceAccountsEnabled": true,
        "authorizationServicesEnabled": false,
        "publicClient": false,
        "protocol": "openid-connect",
        "bearerOnly": false
    }'

    # Frontend public client (SPA / mobile)
    create_client "fincontrol-frontend" '{
        "clientId": "fincontrol-frontend",
        "name": "FinControl Frontend",
        "description": "Client pÃºblico para SPA e apps mobile",
        "enabled": true,
        "publicClient": true,
        "standardFlowEnabled": true,
        "directAccessGrantsEnabled": true,
        "protocol": "openid-connect",
        "redirectUris": ["http://localhost:3000/*", "http://localhost:5173/*"],
        "webOrigins": ["http://localhost:3000", "http://localhost:5173"],
        "attributes": {
            "pkce.code.challenge.method": "S256"
        }
    }'

    # Kong OIDC client (API Gateway)
    create_client "kong-client" '{
        "clientId": "kong-client",
        "name": "Kong API Gateway",
        "description": "Client para o Kong API Gateway",
        "enabled": true,
        "clientAuthenticatorType": "client-secret",
        "secret": "kong-secret",
        "standardFlowEnabled": true,
        "directAccessGrantsEnabled": true,
        "serviceAccountsEnabled": false,
        "publicClient": false,
        "protocol": "openid-connect",
        "redirectUris": ["http://localhost:8000/*"],
        "webOrigins": ["http://localhost:8000"]
    }'
}

################################################################################
# Cria Protocol Mappers para incluir dados do usuÃ¡rio no Access Token
# Garante que name, email, given_name, family_name apareÃ§am no JWT
################################################################################

create_protocol_mappers() {
    print_header "Configurando Protocol Mappers para Clients"

    local clients=("fincontrol-backend" "fincontrol-frontend" "kong-client")

    for client_id in "${clients[@]}"; do
        print_info "Configurando mappers para client: ${client_id}"

        # ObtÃ©m o ID do client
        local client_uuid
        client_uuid=$(curl -s \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients?clientId=${client_id}" \
            | jq -r '.[0].id')

        if [ -z "$client_uuid" ] || [ "$client_uuid" = "null" ]; then
            print_error "Client '${client_id}' nÃ£o encontrado. Pulando mappers."
            continue
        fi

        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        # Mapper: User Attribute - sub (UUID)
        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients/${client_uuid}/protocol-mappers/models" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d '{
                "name": "sub",
                "protocol": "openid-connect",
                "protocolMapper": "oidc-usermodel-attribute-mapper",
                "consentRequired": false,
                "config": {
                    "userinfo.token.claim": "true",
                    "user.attribute": "id",
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "claim.name": "sub",
                    "jsonType.label": "String"
                }
            }' 2>/dev/null || true

        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        # Mapper: User Attribute - name (firstName + lastName)
        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients/${client_uuid}/protocol-mappers/models" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d '{
                "name": "name",
                "protocol": "openid-connect",
                "protocolMapper": "oidc-full-name-mapper",
                "consentRequired": false,
                "config": {
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "userinfo.token.claim": "true"
                }
            }' 2>/dev/null || true

        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        # Mapper: User Attribute - given_name (firstName)
        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients/${client_uuid}/protocol-mappers/models" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d '{
                "name": "given_name",
                "protocol": "openid-connect",
                "protocolMapper": "oidc-usermodel-attribute-mapper",
                "consentRequired": false,
                "config": {
                    "userinfo.token.claim": "true",
                    "user.attribute": "firstName",
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "claim.name": "given_name",
                    "jsonType.label": "String"
                }
            }' 2>/dev/null || true

        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        # Mapper: User Attribute - family_name (lastName)
        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients/${client_uuid}/protocol-mappers/models" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d '{
                "name": "family_name",
                "protocol": "openid-connect",
                "protocolMapper": "oidc-usermodel-attribute-mapper",
                "consentRequired": false,
                "config": {
                    "userinfo.token.claim": "true",
                    "user.attribute": "lastName",
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "claim.name": "family_name",
                    "jsonType.label": "String"
                }
            }' 2>/dev/null || true

        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        # Mapper: User Attribute - email
        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients/${client_uuid}/protocol-mappers/models" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d '{
                "name": "email",
                "protocol": "openid-connect",
                "protocolMapper": "oidc-usermodel-property-mapper",
                "consentRequired": false,
                "config": {
                    "userinfo.token.claim": "true",
                    "user.attribute": "email",
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "claim.name": "email",
                    "jsonType.label": "String"
                }
            }' 2>/dev/null || true

        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        # Mapper: User Attribute - preferred_username (username)
        # â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/clients/${client_uuid}/protocol-mappers/models" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d '{
                "name": "preferred_username",
                "protocol": "openid-connect",
                "protocolMapper": "oidc-usermodel-property-mapper",
                "consentRequired": false,
                "config": {
                    "userinfo.token.claim": "true",
                    "user.attribute": "username",
                    "id.token.claim": "true",
                    "access.token.claim": "true",
                    "claim.name": "preferred_username",
                    "jsonType.label": "String"
                }
            }' 2>/dev/null || true

        print_success "Mappers criados para client '${client_id}'"
    done
}

################################################################################
# Cria Roles no Realm
################################################################################

create_roles() {
    print_header "Criando Roles"

    for role in "fincontrol-admin" "fincontrol-user" "fincontrol-readonly" "fincontrol-accountant"; do
        local existing
        existing=$(curl -s \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/roles/${role}" \
            -o /dev/null -w "%{http_code}")

        if [ "$existing" = "200" ]; then
            print_warning "Role '${role}' jÃ¡ existe. Pulando."
            continue
        fi

        curl -s -X POST "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/roles" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d "{\"name\": \"${role}\", \"description\": \"Role ${role}\"}" > /dev/null

        print_success "Role '${role}' criada!"
    done
}

################################################################################
# Cria um usuÃ¡rio de teste
################################################################################

create_user() {
    local username="$1"
    local email="$2"
    local first_name="$3"
    local last_name="$4"
    local password="$5"
    local role="$6"

    # Verifica se jÃ¡ existe
    local existing
    existing=$(curl -s \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/users?username=${username}" | jq length)

    if [ "$existing" -gt "0" ]; then
        print_warning "UsuÃ¡rio '${username}' jÃ¡ existe. Pulando."
        return 0
    fi

    # Cria o usuÃ¡rio
    local location
    location=$(curl -s -i -X POST "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/users" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        -H "Content-Type: application/json" \
        -d "{
            \"username\": \"${username}\",
            \"email\": \"${email}\",
            \"firstName\": \"${first_name}\",
            \"lastName\": \"${last_name}\",
            \"enabled\": true,
            \"emailVerified\": true,
            \"credentials\": [{
                \"type\": \"password\",
                \"value\": \"${password}\",
                \"temporary\": false
            }]
        }" | grep -i "^location:" | tr -d '\r' | awk '{print $2}')

    local user_id
    user_id=$(basename "$location")

    if [ -z "$user_id" ] || [ "$user_id" = "" ]; then
        print_error "Falha ao criar usuÃ¡rio '${username}'"
        return 1
    fi

    # Busca o ID da role
    local role_id
    role_id=$(curl -s \
        -H "Authorization: Bearer ${ADMIN_TOKEN}" \
        "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/roles/${role}" | jq -r '.id')

    if [ -n "$role_id" ] && [ "$role_id" != "null" ]; then
        curl -s -X POST \
            "${KEYCLOAK_URL}/admin/realms/${REALM_NAME}/users/${user_id}/role-mappings/realm" \
            -H "Authorization: Bearer ${ADMIN_TOKEN}" \
            -H "Content-Type: application/json" \
            -d "[{\"id\": \"${role_id}\", \"name\": \"${role}\"}]" > /dev/null
        print_success "UsuÃ¡rio '${username}' criado com role '${role}'!"
    else
        print_success "UsuÃ¡rio '${username}' criado (sem role atribuÃ­da)!"
    fi
}

################################################################################
# Cria UsuÃ¡rios de Teste
################################################################################

create_test_users() {
    print_header "Criando UsuÃ¡rios de Teste"

    create_user "admin.fincontrol" \
        "admin@fincontrol.local" \
        "Admin" "FinControl" \
        "Admin@123456" \
        "fincontrol-admin"

    create_user "user.fincontrol" \
        "user@fincontrol.local" \
        "User" "FinControl" \
        "User@123456" \
        "fincontrol-user"

    create_user "contador.fincontrol" \
        "contador@fincontrol.local" \
        "Contador" "FinControl" \
        "Contador@123456" \
        "fincontrol-accountant"

    create_user "readonly.fincontrol" \
        "readonly@fincontrol.local" \
        "ReadOnly" "FinControl" \
        "Readonly@123456" \
        "fincontrol-readonly"
}

################################################################################
# Resumo Final
################################################################################

print_summary() {
    print_header "âœ… Keycloak Inicializado com Sucesso!"

    echo -e "${GREEN}Realm:${NC}        ${REALM_NAME}"
    echo -e "${GREEN}URL Admin:${NC}    ${KEYCLOAK_URL}/admin/master/console/#/${REALM_NAME}"
    echo -e ""
    echo -e "${GREEN}Clients criados:${NC}"
    echo -e "  â€¢ fincontrol-backend  (secret: fincontrol-backend-secret-12345)"
    echo -e "  â€¢ fincontrol-frontend (public, PKCE)"
    echo -e "  â€¢ kong-client         (secret: kong-secret)"
    echo -e ""
    echo -e "${GREEN}UsuÃ¡rios de teste:${NC}"
    echo -e "  â€¢ admin.fincontrol   / Admin@123456    (fincontrol-admin)"
    echo -e "  â€¢ user.fincontrol    / User@123456     (fincontrol-user)"
    echo -e "  â€¢ contador.fincontrol/ Contador@123456 (fincontrol-accountant)"
    echo -e "  â€¢ readonly.fincontrol/ Readonly@123456 (fincontrol-readonly)"
    echo -e ""
    echo -e "${GREEN}Claims no Access Token (JWT):${NC}"
    echo -e "  âœ“ sub (UUID do usuÃ¡rio â€” formato GUID)"
    echo -e "  âœ“ name (firstName + lastName)"
    echo -e "  âœ“ given_name (firstName)"
    echo -e "  âœ“ family_name (lastName)"
    echo -e "  âœ“ email"
    echo -e "  âœ“ preferred_username (username)"
    echo -e ""
    echo -e "${GREEN}OIDC Discovery:${NC}"
    echo -e "  ${KEYCLOAK_URL}/realms/${REALM_NAME}/.well-known/openid-configuration"
    echo -e ""
    echo -e "${YELLOW}ðŸ’¡ Teste o JWT em:${NC} https://jwt.io"
}

################################################################################
# ExecuÃ§Ã£o Principal
################################################################################

main() {
    print_header "FinControl - Keycloak Init"

    wait_for_keycloak
    get_admin_token
    create_realm

    # Renova token apÃ³s criar realm (pode ter expirado)
    get_admin_token

    create_clients
    create_protocol_mappers
    create_roles
    create_test_users
    print_summary
}

main "$@"
