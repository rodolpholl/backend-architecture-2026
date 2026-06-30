#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Testa a inicialização do Vault e valida a criação de secrets
.DESCRIPTION
    Script que:
    1. Verifica o status dos containers
    2. Aguarda vault-init completar
    3. Valida secrets foram criados
    4. Testa conectividade com os serviços
.EXAMPLE
    .\Test-VaultInitialization.ps1
#>

param(
    [int]$MaxWaitSeconds = 300,
    [int]$CheckIntervalSeconds = 5
)

# Cores
$Colors = @{
    Success = [ConsoleColor]::Green
    Error = [ConsoleColor]::Red
    Warning = [ConsoleColor]::Yellow
    Info = [ConsoleColor]::Cyan
}

function Write-Step {
    param([string]$Message, [string]$Status = "info")
    $emoji = @{
        info = "ℹ️ "
        success = "✅"
        error = "❌"
        warning = "⚠️ "
    }
    Write-Host $emoji[$Status] -NoNewline -ForegroundColor $Colors[$Status]
    Write-Host "  $Message" -ForegroundColor $Colors[$Status]
}

function Test-ContainerStatus {
    Write-Step "Verificando status dos containers..." -Status "info"
    
    $output = docker-compose ps --format json | ConvertFrom-Json
    
    $containers = @{
        vault = $false
        postgres = $false
        redis = $false
        rabbitmq = $false
        keycloak = $false
        kong = $false
        "vault-init" = $false
    }
    
    foreach ($container in $output) {
        $name = $container.Name
        $state = $container.State
        
        if ($containers.ContainsKey($name)) {
            $containers[$name] = $state
            $status = if ($state -eq "running") { "success" } else { "warning" }
            Write-Step "$name : $state" -Status $status
        }
    }
    
    return $containers
}

function Wait-ForVaultInit {
    param([int]$MaxSeconds)
    
    Write-Step "Aguardando vault-init completar..." -Status "info"
    
    $elapsed = 0
    while ($elapsed -lt $MaxSeconds) {
        $output = docker-compose logs vault-init 2>$null
        
        if ($output -match "✅") {
            Write-Step "vault-init completou com sucesso!" -Status "success"
            return $true
        }
        
        if ($output -match "❌" -or $output -match "Erro") {
            Write-Step "vault-init falhou!" -Status "error"
            Write-Host $output
            return $false
        }
        
        Write-Host "." -NoNewline -ForegroundColor $Colors.Info
        Start-Sleep -Seconds $CheckIntervalSeconds
        $elapsed += $CheckIntervalSeconds
    }
    
    Write-Step "Timeout esperando vault-init" -Status "error"
    return $false
}

function Test-VaultSecrets {
    Write-Step "Validando secrets no Vault..." -Status "info"
    
    $secrets = @("postgres", "redis", "rabbitmq", "grafana", "vault")
    $vaultAddr = "http://localhost:8200"
    $token = "agile_dev_token_12345"
    
    foreach ($secret in $secrets) {
        try {
            $response = curl.exe -s `
                -H "X-Vault-Token: $token" `
                "$vaultAddr/v1/secret/data/dev/$secret"
            
            if ($response -match '"data"') {
                Write-Step "Secret '$secret' encontrado" -Status "success"
            } else {
                Write-Step "Secret '$secret' NÃO encontrado" -Status "error"
            }
        } catch {
            Write-Step "Erro ao verificar '$secret': $_" -Status "error"
        }
    }
}

function Test-Connectivity {
    Write-Step "Testando conectividade com os serviços..." -Status "info"
    
    $services = @{
        "Vault" = "http://localhost:8200/v1/sys/health"
        "Keycloak" = "http://localhost:8081/health/ready"
        "Kong Admin" = "http://localhost:8001/status"
        "Prometheus" = "http://localhost:9090/-/healthy"
        "Grafana" = "http://localhost:3000/api/health"
    }
    
    foreach ($service in $services.GetEnumerator()) {
        try {
            $response = curl.exe -s -f $service.Value 2>$null
            if ($response) {
                Write-Step "$($service.Name) está acessível" -Status "success"
            } else {
                Write-Step "$($service.Name) não respondeu" -Status "warning"
            }
        } catch {
            Write-Step "$($service.Name) inacessível" -Status "error"
        }
    }
}

function Show-Summary {
    Write-Host "`n"
    Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  📊 Resumo da Inicialização" -ForegroundColor Cyan
    Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Step "Próximos passos:" -Status "info"
    Write-Host "   1. Acessar Vault UI: http://localhost:8200/ui" -ForegroundColor White
    Write-Host "   2. Token: agile_dev_token_12345" -ForegroundColor White
    Write-Host "   3. Navegar: secret/ → dev/" -ForegroundColor White
    Write-Host ""
    
    Write-Step "Cleanup (opcional):" -Status "info"
    Write-Host "   docker-compose rm -f vault-init kong-init" -ForegroundColor White
    Write-Host ""
}

# ============================================================================
# Main
# ============================================================================

Write-Host ""
Write-Host "🔐 Teste de Inicialização do Vault" -ForegroundColor Cyan
Write-Host ""

# 1. Verificar status
$containers = Test-ContainerStatus
Write-Host ""

# 2. Aguardar vault-init
$initSuccess = Wait-ForVaultInit -MaxSeconds $MaxWaitSeconds
Write-Host ""

if ($initSuccess) {
    # 3. Validar secrets
    Test-VaultSecrets
    Write-Host ""
    
    # 4. Testar conectividade
    Test-Connectivity
    Write-Host ""
    
    # 5. Resumo
    Show-Summary
} else {
    Write-Step "Inicialização falhou. Verifique os logs:" -Status "error"
    Write-Host "docker-compose logs vault-init" -ForegroundColor Yellow
    Write-Host "docker-compose logs vault" -ForegroundColor Yellow
}
