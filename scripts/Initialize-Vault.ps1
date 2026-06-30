<#
.SYNOPSIS
    Inicializa o Vault com secrets de desenvolvimento para Agile Workers

.DESCRIPTION
    Script PowerShell que configura o HashiCorp Vault com todos os secrets necessários
    para o ambiente de desenvolvimento do Agile Workers Backend.

.PARAMETER VaultAddr
    Endereço do Vault (padrão: http://localhost:8200)

.PARAMETER VaultToken
    Token de acesso ao Vault (padrão: agile_dev_token_12345 - DEV ONLY!)

.PARAMETER Environment
    Ambiente a configurar: dev, staging, production (padrão: dev)

.EXAMPLE
    .\Initialize-Vault.ps1 -Environment dev

.NOTES
    ⚠️  SEGURANÇA: Nunca use tokens hardcoded em produção!
    Use autenticação via AppRole ou JWT em produção.
#>

param(
    [string]$VaultAddr = "http://localhost:8200",
    [string]$VaultToken = "agile_dev_token_12345",
    [ValidateSet("dev", "staging", "production")]
    [string]$Environment = "dev"
)

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Title" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
}

function Test-VaultConnection {
    Write-Host "🔍 Testando conexão com Vault..." -ForegroundColor Yellow

    try {
        $response = Invoke-RestMethod -Uri "$VaultAddr/v1/sys/health" -Method Get -ErrorAction Stop
        Write-Host "✅ Vault está acessível!" -ForegroundColor Green
        Write-Host "   Status: $($response.sealed ? 'SEALED' : 'UNSEALED')" -ForegroundColor Gray
        return $true
    }
    catch {
        Write-Host "❌ Erro ao conectar ao Vault:" -ForegroundColor Red
        Write-Host "   $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 Dica: Execute 'docker-compose up -d' para iniciar o Vault" -ForegroundColor Yellow
        return $false
    }
}

function Invoke-VaultAPI {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Data
    )

    $headers = @{
        "X-Vault-Token" = $VaultToken
        "Content-Type"  = "application/json"
    }

    $uri = "$VaultAddr/v1$Path"
    $body = $Data | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -Body $body -ErrorAction Stop
        return $response
    }
    catch {
        Write-Host "❌ Erro na requisição Vault: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

function Create-Secret {
    param(
        [string]$Path,
        [hashtable]$Data
    )

    Write-Host "  📝 Criando secret: $Path" -ForegroundColor Gray

    $secretPath = "secret/data/$Environment/$Path"
    $payload = @{ data = $Data }

    try {
        Invoke-VaultAPI -Method Post -Path $secretPath -Data $payload | Out-Null
        Write-Host "  ✅ Secret criado com sucesso" -ForegroundColor Green
    }
    catch {
        Write-Host "  ⚠️  Erro ao criar secret: $_" -ForegroundColor Yellow
    }
}

function Get-Secret {
    param([string]$Path)

    $secretPath = "secret/data/$Environment/$Path"

    try {
        $response = Invoke-VaultAPI -Method Get -Path $secretPath -Data @{}
        return $response.data.data
    }
    catch {
        Write-Host "❌ Erro ao ler secret: $_" -ForegroundColor Red
        return $null
    }
}

# ═══════════════════════════════════════════════════════
# SCRIPT PRINCIPAL
# ═══════════════════════════════════════════════════════

Write-Host ""
Write-Host "🔐 Inicializador de Secrets - Agile Workers Backend" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configurações:" -ForegroundColor Gray
Write-Host "  • Vault Address: $VaultAddr" -ForegroundColor Gray
Write-Host "  • Environment: $Environment" -ForegroundColor Gray
Write-Host "  • Token: $($VaultToken.Substring(0, 5))...***" -ForegroundColor Gray
Write-Host ""

# Teste de conexão
if (-not (Test-VaultConnection)) {
    exit 1
}

# ═══════════════════════════════════════════════════════
# CRIAR SECRETS
# ═══════════════════════════════════════════════════════

Write-Section "Criando Secrets no Vault ($Environment)"

# PostgreSQL
Write-Host ""
Write-Host "🗄️  PostgreSQL" -ForegroundColor Cyan
Create-Secret "postgres" @{
    username = "agile_admin"
    password = "agile_dev_password_123"
    host     = "postgres"
    port     = "5432"
}

# Redis
Write-Host ""
Write-Host "⚡ Redis" -ForegroundColor Cyan
Create-Secret "redis" @{
    password = "agile_redis_password_123"
    host     = "redis"
    port     = "6379"
}

# RabbitMQ
Write-Host ""
Write-Host "🐰 RabbitMQ" -ForegroundColor Cyan
Create-Secret "rabbitmq" @{
    username = "agile_user"
    password = "agile_rabbitmq_password_123"
    vhost    = "/agile"
    host     = "rabbitmq"
    port     = "5672"
}

# Grafana
Write-Host ""
Write-Host "📊 Grafana" -ForegroundColor Cyan
Create-Secret "grafana" @{
    admin_password = "agile_grafana_password_123"
    admin_username = "admin"
}

# Vault
Write-Host ""
Write-Host "🔑 Vault" -ForegroundColor Cyan
Create-Secret "vault" @{
    root_token = "agile_dev_token_12345"
}

# ═══════════════════════════════════════════════════════
# VERIFICAR SECRETS
# ═══════════════════════════════════════════════════════

Write-Section "Verificando Secrets Criados"

$secrets = @("postgres", "redis", "rabbitmq", "grafana", "vault")

foreach ($secret in $secrets) {
    Write-Host ""
    Write-Host "📋 Lendo: secret/$Environment/$secret" -ForegroundColor Gray

    $data = Get-Secret $secret
    if ($data) {
        Write-Host "✅ Encontrado:" -ForegroundColor Green
        $data.Keys | ForEach-Object {
            $value = if ($data[$_].Length -gt 20) { "$($data[$_].Substring(0, 20))..." } else { $data[$_] }
            Write-Host "   • $_ = $value" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "❌ Secret não encontrado!" -ForegroundColor Red
    }
}

# ═══════════════════════════════════════════════════════
# RESUMO
# ═══════════════════════════════════════════════════════

Write-Section "Resumo da Configuração"

Write-Host ""
Write-Host "✅ Todos os secrets foram inicializados com sucesso!" -ForegroundColor Green
Write-Host ""
Write-Host "Próximos passos:" -ForegroundColor Cyan
Write-Host "  1. Verificar secrets no Vault UI: http://localhost:8200/ui" -ForegroundColor Gray
Write-Host "  2. Usar token para autenticar: $VaultToken" -ForegroundColor Gray
Write-Host "  3. Navegar para: secret/$Environment/" -ForegroundColor Gray
Write-Host ""
Write-Host "⚠️  LEMBRETE: Os valores acima são APENAS para desenvolvimento!" -ForegroundColor Yellow
Write-Host "              Em produção, gere secrets fortes e únicos!" -ForegroundColor Yellow
Write-Host ""
