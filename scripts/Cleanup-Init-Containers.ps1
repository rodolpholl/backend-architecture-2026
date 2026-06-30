<#
.SYNOPSIS
    Remove os containers de inicialização (vault-init e kong-init) após execução bem-sucedida

.DESCRIPTION
    Script que remove os containers temporários de inicialização que já completaram sua execução.
    Estes containers são necessários apenas para configuração inicial do Vault e Kong.

.PARAMETER RemoveNetworks
    Se $true, também remove a rede Docker (use com cuidado)

.EXAMPLE
    .\Cleanup-Init-Containers.ps1
    
.EXAMPLE
    .\Cleanup-Init-Containers.ps1 -RemoveNetworks $true

.NOTES
    ⚠️  ATENÇÃO: Verifique os logs antes de remover para garantir sucesso da inicialização
#>

param(
    [bool]$RemoveNetworks = $false
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "🧹 Limpando containers de inicialização..." -ForegroundColor Cyan
Write-Host ""

# Containers a remover
$containersToRemove = @(
    "agile-vault-init",
    "agile-kong-init"
)

$removedCount = 0

foreach ($containerName in $containersToRemove) {
    try {
        # Verifica se container existe
        $container = docker ps -a --filter "name=^${containerName}$" --format "{{.ID}}"
        
        if ($container) {
            Write-Host "  🗑️  Removendo container: $containerName" -ForegroundColor Yellow
            docker rm -f $containerName | Out-Null
            Write-Host "  ✅ Container removido: $containerName" -ForegroundColor Green
            $removedCount++
        }
        else {
            Write-Host "  ℹ️  Container não encontrado: $containerName" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  ❌ Erro ao remover $containerName : $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "📊 Resumo:" -ForegroundColor Cyan
Write-Host "  • Containers removidos: $removedCount"
Write-Host "  • Containers analisados: $($containersToRemove.Count)"
Write-Host ""

# Exibe logs dos containers removidos (se disponíveis)
Write-Host "📋 Logs da última execução:" -ForegroundColor Cyan
Write-Host ""

Write-Host "▸ Vault Init:" -ForegroundColor Yellow
docker logs agile-vault-init 2>&1 | Select-Object -Last 10
Write-Host ""

Write-Host "▸ Kong Init:" -ForegroundColor Yellow
docker logs agile-kong-init 2>&1 | Select-Object -Last 10
Write-Host ""

Write-Host "✨ Limpeza concluída!" -ForegroundColor Green
Write-Host ""
Write-Host "Próximos passos:" -ForegroundColor Cyan
Write-Host "  1. Verifique os logs acima para confirmar sucesso"
Write-Host "  2. Execute: docker-compose ps"
Write-Host "  3. Todos os containers principais devem estar 'Up (healthy)'"
Write-Host ""
