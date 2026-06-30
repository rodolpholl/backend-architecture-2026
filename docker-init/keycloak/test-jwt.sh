#!/bin/bash

################################################################################
# Script para testar JWT do Keycloak - FinControl
# Valida se os Protocol Mappers estão funcionando corretamente
# 
# Uso: ./test-jwt.sh [username] [password]
# Exemplo: ./test-jwt.sh admin.fincontrol Admin@123456
################################################################################

set -euo pipefail

# Configuração padrão
KEYCLOAK_URL="${KEYCLOAK_URL:-http://localhost:8080}"
REALM="${REALM:-fincontrol}"
CLIENT_ID="${CLIENT_ID:-fincontrol-backend}"
CLIENT_SECRET="${CLIENT_SECRET:-fincontrol-backend-secret-12345}"

# Argumentos
USERNAME="${1:-admin.fincontrol}"
PASSWORD="${2:-Admin@123456}"

# Cores
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BLUE='\033[0;34m'
NC='\033[0m'

print_header() { echo -e "\n${CYAN}════════════════════════════════════════════════════════${NC}\n${CYAN}  $1${NC}\n${CYAN}════════════════════════════════════════════════════════${NC}\n"; }
print_info()    { echo -e "${CYAN}ℹ${NC}  $1"; }
print_success() { echo -e "${GREEN}✓${NC}  $1"; }
print_error()   { echo -e "${RED}✗${NC}  $1"; }
print_warning() { echo -e "${YELLOW}⚠${NC}  $1"; }

################################################################################
# Obter Token
################################################################################

get_token() {
    print_info "Obtendo Access Token do Keycloak..."
    print_info "URL: ${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token"
    print_info "Usuário: ${USERNAME}"

    local response
    response=$(curl -s -X POST \
        "${KEYCLOAK_URL}/realms/${REALM}/protocol/openid-connect/token" \
        -H "Content-Type: application/x-www-form-urlencoded" \
        -d "grant_type=password" \
        -d "client_id=${CLIENT_ID}" \
        -d "client_secret=${CLIENT_SECRET}" \
        -d "username=${USERNAME}" \
        -d "password=${PASSWORD}" \
        -d "scope=openid profile email")

    local token
    token=$(echo "$response" | jq -r '.access_token // empty' 2>/dev/null || echo "")

    if [ -z "$token" ]; then
        print_error "Falha ao obter token!"
        echo ""
        echo "$response" | jq '.' 2>/dev/null || echo "$response"
        exit 1
    fi

    echo "$token"
}

################################################################################
# Decodificar JWT
################################################################################

decode_jwt() {
    local token="$1"
    echo "$token" | jq -R 'split(".") | .[1] | @base64d | fromjson'
}

################################################################################
# Validar Claims
################################################################################

validate_claims() {
    local payload="$1"
    
    print_header "📋 Validação de Claims"

    local required_claims=("sub" "name" "given_name" "family_name" "email" "preferred_username")
    local missing=0

    for claim in "${required_claims[@]}"; do
        local value
        value=$(echo "$payload" | jq -r ".$claim // empty" 2>/dev/null || echo "")

        if [ -n "$value" ]; then
            print_success "$claim: ${BLUE}$value${NC}"
        else
            print_error "$claim: AUSENTE ❌"
            ((missing++))
        fi
    done

    echo ""
    if [ $missing -eq 0 ]; then
        print_success "Todas as claims obrigatórias estão presentes! ✅"
        return 0
    else
        print_error "$missing claim(s) ausente(s). Verifique os Protocol Mappers."
        return 1
    fi
}

################################################################################
# Exibir Token Completo (Decoded)
################################################################################

display_full_jwt() {
    local payload="$1"
    
    print_header "🔐 JWT Completo (Decoded)"
    echo "$payload" | jq '.'
}

################################################################################
# Gerar QR Code para jwt.io (opcional)
################################################################################

suggest_online_validation() {
    local token="$1"
    
    echo ""
    print_info "Para validação online, visite:"
    echo -e "  ${BLUE}https://jwt.io${NC}"
    echo ""
    print_info "Cole o token abaixo no campo Encoded:"
    echo ""
    echo -e "${YELLOW}${token}${NC}"
    echo ""
}

################################################################################
# Testar Requisição ao Backend
################################################################################

test_backend_request() {
    local token="$1"
    
    print_header "🧪 Teste de Requisição ao Backend (opcional)"
    
    print_info "Para testar o token em uma requisição real, use:"
    echo ""
    echo -e "${YELLOW}curl -H \"Authorization: Bearer ${token}\" \\\\"
    echo -e "  http://localhost:5000/lancamentos/registrar${NC}"
    echo ""
}

################################################################################
# Main
################################################################################

main() {
    print_header "🔐 Teste de JWT - Keycloak FinControl"

    print_info "Configuração:"
    echo "  • Keycloak URL: ${KEYCLOAK_URL}"
    echo "  • Realm: ${REALM}"
    echo "  • Client ID: ${CLIENT_ID}"
    echo "  • Usuário: ${USERNAME}"
    echo ""

    # Obter token
    print_info "Obtendo token..."
    local token
    token=$(get_token)
    print_success "Token obtido com sucesso!"

    echo ""

    # Decodificar
    print_info "Decodificando JWT..."
    local payload
    payload=$(decode_jwt "$token")

    echo ""

    # Exibir payload completo
    display_full_jwt "$payload"

    echo ""

    # Validar claims
    validate_claims "$payload"
    local validation_result=$?

    echo ""

    # Sugerir validação online
    suggest_online_validation "$token"

    # Sugerir teste ao backend
    test_backend_request "$token"

    if [ $validation_result -eq 0 ]; then
        print_header "✅ Tudo OK! O Keycloak está configurado corretamente."
    else
        print_header "⚠️  Verifique a configuração dos Protocol Mappers no Keycloak"
    fi
}

main "$@"
