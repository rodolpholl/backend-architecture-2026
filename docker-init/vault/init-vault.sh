#!/bin/bash

################################################################################
# Inicializa o Vault com secrets de desenvolvimento para FinControl
#
# Variáveis de ambiente:
#   VAULT_ADDR      - Endereço do Vault (padrão: http://vault:8200)
#   VAULT_TOKEN     - Token de acesso (padrão: fincontrol_dev_token_12345)
#   VAULT_ENV       - Ambiente: dev, staging, production (padrão: dev)
################################################################################

set -euo pipefail

# Configurações padrão
VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
VAULT_TOKEN="${VAULT_TOKEN:-fincontrol_dev_token_12345}"
VAULT_ENV="${VAULT_ENV:-dev}"

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

################################################################################
# Funções Auxiliares
################################################################################

print_header() {
    echo -e "\n${CYAN}════════════════════════════════════════════════════════${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}════════════════════════════════════════════════════════${NC}\n"
}

print_info() {
    echo -e "${CYAN}ℹ${NC}  $1"
}

print_success() {
    echo -e "${GREEN}✓${NC}  $1"
}

print_error() {
    echo -e "${RED}✗${NC}  $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC}  $1"
}

################################################################################
# Testa Conexão com Vault
################################################################################

test_vault_connection() {
    print_info "Testando conexão com Vault..."
    
    if ! response=$(curl -s -f "${VAULT_ADDR}/v1/sys/health" 2>/dev/null); then
        print_error "Erro ao conectar ao Vault em ${VAULT_ADDR}"
        echo -e "${YELLOW}💡 Dica: Verifique se o Vault está rodando e accessible${NC}\n"
        return 1
    fi
    
    print_success "Vault está acessível!"
    return 0
}

################################################################################
# Cria um Secret no Vault
################################################################################

create_secret() {
    local secret_name="$1"
    shift
    local -a secret_data=("$@")
    
    # Monta o JSON dinamicamente
    local json="{\"data\": {"
    local first=true
    
    for pair in "${secret_data[@]}"; do
        IFS='=' read -r key value <<< "$pair"
        if [ "$first" = true ]; then
            json="${json}\"${key}\": \"${value}\""
            first=false
        else
            json="${json}, \"${key}\": \"${value}\""
        fi
    done
    
    json="${json}}}"
    
    print_info "Criando secret: ${secret_name}"
    
    local secret_path="secret/data/${VAULT_ENV}/${secret_name}"
    local url="${VAULT_ADDR}/v1/${secret_path}"
    
    local response
    response=$(curl -s -w "\n%{http_code}" -X POST \
        -H "X-Vault-Token: ${VAULT_TOKEN}" \
        -H "Content-Type: application/json" \
        -d "${json}" \
        "${url}" 2>&1)
    
    local http_code
    http_code=$(echo "${response}" | tail -1)
    local body
    body=$(echo "${response}" | head -1)
    
    if [ "${http_code}" = "200" ] || [ "${http_code}" = "204" ]; then
        print_success "Secret '${secret_name}' criado (HTTP ${http_code})"
        return 0
    else
        print_error "Falha ao criar '${secret_name}' (HTTP ${http_code}): ${body}"
        return 1
    fi
}

################################################################################
# Lê um Secret do Vault
################################################################################

read_secret() {
    local secret_name="$1"
    local secret_path="secret/data/${VAULT_ENV}/${secret_name}"
    
    curl -s -H "X-Vault-Token: ${VAULT_TOKEN}" \
        "${VAULT_ADDR}/v1/${secret_path}" 2>/dev/null || echo "{}"
}

################################################################################
# EXECUÇÃO PRINCIPAL
################################################################################

main() {
    echo ""
    echo -e "${CYAN}🔐 Inicializador de Secrets - FinControl${NC}"
    echo ""
    print_info "Configurações:"
    echo -e "   • Vault Address: ${VAULT_ADDR}"
    echo -e "   • Environment: ${VAULT_ENV}"
    echo -e "   • Token: ${VAULT_TOKEN:0:5}...***\n"
    
    # Teste de conexão
    if ! test_vault_connection; then
        exit 1
    fi
    
    # Criar Secrets
    print_header "Criando Secrets no Vault (${VAULT_ENV})"
    
    # PostgreSQL — chave esperada por VaultKeys.PostgresConnection
    echo -e "${CYAN}🗄️  PostgreSQL${NC}"
    create_secret "postgres" \
        "connection_string=Host=localhost;Port=5432;Database=fincontrol_lancamentos;Username=fincontrol_admin;Password=fincontrol_dev_password_123" \
        "username=fincontrol_admin" \
        "password=fincontrol_dev_password_123" \
        "host=localhost" \
        "port=5432"
    echo ""

    # Redis — chave esperada por VaultKeys.RedisConnection
    echo -e "${CYAN}⚡ Redis${NC}"
    create_secret "redis" \
        "connection_string=localhost:6379,password=fincontrol_redis_password_123,abortConnect=false" \
        "password=fincontrol_redis_password_123" \
        "host=localhost" \
        "port=6379"
    echo ""

    # RabbitMQ — chave esperada por VaultKeys.RabbitMqUri
    echo -e "${CYAN}🐰 RabbitMQ${NC}"
    create_secret "rabbitmq" \
        "uri=amqp://fincontrol_user:fincontrol_rabbitmq_password_123@localhost:5672/%2Ffincontrol" \
        "username=fincontrol_user" \
        "password=fincontrol_rabbitmq_password_123" \
        "vhost=/fincontrol" \
        "host=localhost" \
        "port=5672"
    echo ""

    # Grafana — chaves esperadas por VaultKeys.LokiUrl / OtlpEndpoint / PrometheusPushgateway
    echo -e "${CYAN}📊 Grafana${NC}"
    create_secret "grafana" \
        "loki_url=http://localhost:3100" \
        "otlp_endpoint=http://localhost:4317" \
        "prometheus_pushgateway=http://localhost:9091" \
        "admin_username=admin" \
        "admin_password=fincontrol_grafana_password_123"
    echo ""

    # Keycloak — chaves esperadas por VaultKeys.Keycloak*
    echo -e "${CYAN}🔐 Keycloak${NC}"
    create_secret "keycloak" \
        "realm=fincontrol" \
        "url=http://localhost:8081" \
        "issuer=http://localhost:8081/realms/fincontrol" \
        "jwks_uri=http://localhost:8081/realms/fincontrol/protocol/openid-connect/certs" \
        "admin_username=admin" \
        "admin_password=fincontrol_keycloak_password_123" \
        "kong_client_id=kong-client" \
        "kong_client_secret=kong-secret" \
        "api_client_id=fincontrol-api" \
        "api_client_secret=fincontrol-api-secret" \
        "dev_user=dev.user" \
        "dev_user_password=Dev@123456!" \
        "dev_admin=dev.admin" \
        "dev_admin_password=Admin@123456!"
    echo ""

    # Kong — subscription keys para autenticação no API Gateway
    # Cada API tem sua própria key para controle e rastreamento granular.
    # Header esperado pelo Kong: X-Subscription-Key
    echo -e "${CYAN}🔑 Kong${NC}"
    create_secret "kong" \
        "lancamentos_subscription_key=fc-lanc-dev-subkey-2026-abc123ef" \
        "consolidados_subscription_key=fc-cons-dev-subkey-2026-xyz789ab"
    echo ""

    # Vault (metadados / AppRole para produção)
    echo -e "${CYAN}🔑 Vault${NC}"
    create_secret "vault" \
        "root_token=fincontrol_dev_token_12345"
    echo ""
    
    # Verificar Secrets Criados
    print_header "Verificando Secrets Criados"
    
    for secret in postgres redis rabbitmq grafana keycloak kong vault; do
        echo -e "${CYAN}📋 Lendo: secret/${VAULT_ENV}/${secret}${NC}"
        
        response=$(read_secret "$secret")
        
        if echo "$response" | grep -q '"data"'; then
            print_success "Encontrado:"
            echo "$response" | grep -o '"[^"]*":"[^"]*"' | head -5 | sed 's/^/   • /' | sed 's/":"/: /' | sed 's/"//g'
        else
            print_error "Secret não encontrado!"
        fi
        echo ""
    done
    
    # Resumo
    print_header "Resumo da Configuração"
    
    print_success "Todos os secrets foram inicializados com sucesso!"
    echo ""
    print_info "Próximos passos:"
    echo -e "   1. Acessar Vault UI: ${VAULT_ADDR}/ui"
    echo -e "   2. Usar token: ${VAULT_TOKEN}"
    echo -e "   3. Navegar para: secret/${VAULT_ENV}/"
    echo ""
    print_warning "LEMBRETE: Os valores acima são APENAS para desenvolvimento!"
    print_warning "          Em produção, gere secrets fortes e únicos!"
    echo ""
}

# Executa
main
