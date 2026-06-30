using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinControl.Infrastructure.Vault;

/// <summary>
/// Extension methods para registrar o HashiCorp Vault como fonte de configuração.
///
/// ORDEM RECOMENDADA no Program.cs:
///
///   var builder = WebApplication.CreateBuilder(args);
///
///   // 1. Adicionar Vault ANTES de qualquer outro serviço que precise dos secrets
///   builder.AddFinControlVault();
///
///   // 2. Registrar serviços que dependem dos secrets
///   builder.Services.AddDbContext&lt;AppDbContext&gt;(opts =>
///       opts.UseNpgsql(builder.Configuration[VaultKeys.PostgresConnection]));
///
/// PATHS NO VAULT (mount: secret, KV v2) — conforme ARQUITETURA.MD §14:
///   secret/db            → connection_string
///   secret/redis         → connection_string
///   secret/rabbitmq      → uri
///   secret/jwt           → signing_key, authority, audience
///   secret/keycloak      → client_id, client_secret
///   secret/kong          → admin_api_key
///   secret/observability → otlp_endpoint, loki_url, prometheus_pushgateway
///
/// CONFIGURAÇÃO (vault.settings.json — sem dados sensíveis):
/// {
///   "Vault": {
///     "Address": "https://vault.empresa.com:8200",
///     "AuthMethod": "AppRole",
///     "MountPoint": "secret",
///     "SecretPaths": ["db", "redis", "rabbitmq", "jwt", "keycloak", "kong", "observability"],
///     "Required": true
///   }
/// }
///
/// VARIÁVEIS DE AMBIENTE (CI/CD / Kubernetes — dados sensíveis):
///   Vault__RoleId=&lt;role-id&gt;
///   Vault__SecretId=&lt;secret-id&gt;
///
/// DESENVOLVIMENTO LOCAL (vault.settings.Development.json — NÃO commitar):
/// {
///   "Vault": {
///     "AuthMethod": "Token",
///     "Token": "myroot",
///     "Required": false
///   }
/// }
/// 
public static class VaultExtensions
{
    /// <summary>
    /// Adiciona o HashiCorp Vault ao pipeline de configuração do WebApplicationBuilder.
    /// Deve ser chamado ANTES de qualquer serviço que dependa de secrets.
    /// </summary>
    public static WebApplicationBuilder AddFinControlVault(
        this WebApplicationBuilder builder)
    {
        // Carrega vault.settings.json e vault.settings.{Environment}.json ANTES de ler as
        // opções. Esses arquivos definem SecretPaths, Address e AuthMethod sem expor secrets.
        // O arquivo específico do ambiente sobrescreve apenas os campos que diferem.
        builder.Configuration
            .AddJsonFile("vault.settings.json", optional: true, reloadOnChange: false)
            .AddJsonFile(
                $"vault.settings.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: false);

        var vaultOptions = builder.Configuration
            .GetSection(VaultOptions.SectionName)
            .Get<VaultOptions>() ?? new VaultOptions();

        // Sobrescreve com variáveis de ambiente (Vault__RoleId, Vault__SecretId, etc.)
        // já resolvidas pelo pipeline padrão do .NET antes deste ponto.
        builder.Configuration.AddVaultSecrets(vaultOptions);

        // Registra VaultOptions no DI para uso em outros serviços (ex: renovação de token)
        builder.Services.Configure<VaultOptions>(
            builder.Configuration.GetSection(VaultOptions.SectionName));

        return builder;
    }

    /// <summary>
    /// Adiciona a fonte de configuração do Vault diretamente ao IConfigurationBuilder.
    /// Use quando não tiver acesso ao WebApplicationBuilder (ex: testes, workers sem host web).
    /// </summary>
    public static IConfigurationBuilder AddVaultSecrets(
        this IConfigurationBuilder configBuilder,
        VaultOptions options)
    {
        configBuilder.Add(new VaultConfigurationSource(options));
        return configBuilder;
    }
}
