using Microsoft.Extensions.Configuration;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;

namespace FinControl.Infrastructure.Vault;

/// <summary>
/// Provedor de configuração customizado que lê secrets do HashiCorp Vault
/// e os injeta no pipeline de IConfiguration do .NET.
///
/// ────────────────────────────────────────────────────────────────────────────
/// ESTRATÉGIA DE INJEÇÃO DE CHAVES
/// ────────────────────────────────────────────────────────────────────────────
///
/// Cada secret path é lido e suas chaves são injetadas no IConfiguration
/// usando o ÚLTIMO SEGMENTO DO PATH como namespace:
///
///   Path: "dev/postgres"  | Vault key: "connection_string"
///   → IConfiguration key: "postgres:connection_string"
///
///   Path: "dev/redis"     | Vault key: "connection_string"
///   → IConfiguration key: "redis:connection_string"
///
///   Path: "dev/rabbitmq"  | Vault key: "uri"
///   → IConfiguration key: "rabbitmq:uri"
///
///   Path: "dev/grafana"   | Vault key: "loki_url"
///   → IConfiguration key: "grafana:loki_url"
///
/// Isso garante que secrets de paths diferentes não colidam, e que os
/// nomes das constantes em VaultKeys reflitam exatamente a estrutura do Vault.
///
/// ────────────────────────────────────────────────────────────────────────────
/// Se VaultOptions.ConfigurationPrefix estiver preenchido, ele é adicionado
/// como prefixo adicional: "{prefix}:{namespace}:{key}"
/// ────────────────────────────────────────────────────────────────────────────
///
/// Autenticação suportada:
///   - AppRole  (produção)  — RoleId + SecretId
///   - Token    (dev local) — token raiz (ex: "myroot")
/// </summary>
internal sealed class VaultConfigurationProvider(VaultOptions options) : ConfigurationProvider
{
    public override void Load()
    {
        if (options.SecretPaths.Length == 0) return;

        try
        {
            var client = BuildClient();
            LoadSecretsAsync(client).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (!options.Required)
        {
            // Modo não-obrigatório (dev local): loga e continua sem os secrets do Vault
            Console.Error.WriteLine(
                $"[Vault] Falha ao carregar secrets (Required=false): {ex.Message}");
        }
    }

    private async Task LoadSecretsAsync(VaultClient client)
    {
        foreach (var path in options.SecretPaths)
        {
            Secret<SecretData> secret =
                await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: path,
                    mountPoint: options.MountPoint);

            // O último segmento do path vira o namespace das chaves.
            // Ex: "dev/postgres" → namespace = "postgres"
            var pathNamespace = path.Contains('/')
                ? path[(path.LastIndexOf('/') + 1)..]
                : path;

            foreach (var kvp in secret.Data.Data)
            {
                // Normaliza "__" → ":" para hierarquia no IConfiguration
                var normalizedKey = NormalizeKey(kvp.Key);

                // Monta a chave final: [{prefix}:]{namespace}:{key}
                var key = string.IsNullOrEmpty(options.ConfigurationPrefix)
                    ? $"{pathNamespace}:{normalizedKey}"
                    : $"{options.ConfigurationPrefix}:{pathNamespace}:{normalizedKey}";

                Data[key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }
    }

    private VaultClient BuildClient()
    {
        IAuthMethodInfo authMethod = options.AuthMethod.ToUpperInvariant() switch
        {
            "APPROLE" => BuildAppRoleAuth(),
            "TOKEN"   => BuildTokenAuth(),
            _ => throw new InvalidOperationException(
                $"Vault AuthMethod '{options.AuthMethod}' não suportado. Use 'AppRole' ou 'Token'.")
        };

        var settings = new VaultClientSettings(options.Address, authMethod)
        {
            MyHttpClientProviderFunc = null
        };

        return new VaultClient(settings);
    }

    private AppRoleAuthMethodInfo BuildAppRoleAuth()
    {
        if (string.IsNullOrWhiteSpace(options.RoleId))
            throw new InvalidOperationException(
                "Vault:RoleId é obrigatório para autenticação AppRole.");
        if (string.IsNullOrWhiteSpace(options.SecretId))
            throw new InvalidOperationException(
                "Vault:SecretId é obrigatório para autenticação AppRole.");

        return new AppRoleAuthMethodInfo(options.RoleId, options.SecretId);
    }

    private TokenAuthMethodInfo BuildTokenAuth()
    {
        if (string.IsNullOrWhiteSpace(options.Token))
            throw new InvalidOperationException(
                "Vault:Token é obrigatório para autenticação Token.");

        return new TokenAuthMethodInfo(options.Token);
    }

    /// <summary>
    /// Normaliza chaves do Vault para o formato hierárquico do IConfiguration.
    /// Ex: "connection__string" → "connection:string"
    /// </summary>
    private static string NormalizeKey(string key) =>
        key.Replace("__", ":", StringComparison.Ordinal);
}

/// <summary>Source que expõe o provider ao pipeline do IConfiguration.</summary>
internal sealed class VaultConfigurationSource(VaultOptions options) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new VaultConfigurationProvider(options);
}
