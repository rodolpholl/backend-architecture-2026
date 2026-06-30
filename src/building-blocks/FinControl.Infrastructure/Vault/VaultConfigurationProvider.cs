using Microsoft.Extensions.Configuration;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;

namespace FinControl.Infrastructure.Vault;

/// <summary>
/// Custom configuration provider that reads secrets from HashiCorp Vault
/// and injects them into the .NET IConfiguration pipeline.
///
/// ────────────────────────────────────────────────────────────────────────────
/// KEY INJECTION STRATEGY
/// ────────────────────────────────────────────────────────────────────────────
///
/// Each secret path is read and its keys are injected into IConfiguration
/// using the LAST SEGMENT OF THE PATH as namespace:
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
/// This ensures that secrets from different paths don't collide, and that the
/// names of constants in VaultKeys exactly reflect the structure of the Vault.
///
/// ────────────────────────────────────────────────────────────────────────────
/// If VaultOptions.ConfigurationPrefix is populated, it is added
/// as an additional prefix: "{prefix}:{namespace}:{key}"
/// ────────────────────────────────────────────────────────────────────────────
///
/// Supported authentication:
///   - AppRole  (production)  — RoleId + SecretId
///   - Token    (local dev)   — root token (ex: "myroot")
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
            // Optional mode (local dev): logs and continues without Vault secrets
            Console.Error.WriteLine(
                $"[Vault] Failed to load secrets (Required=false): {ex.Message}");
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

            // The last segment of the path becomes the namespace for the keys.
            // Ex: "dev/postgres" → namespace = "postgres"
            var pathNamespace = path.Contains('/')
                ? path[(path.LastIndexOf('/') + 1)..]
                : path;

            foreach (var kvp in secret.Data.Data)
            {
                // Normalizes "__" → ":" for hierarchy in IConfiguration
                var normalizedKey = NormalizeKey(kvp.Key);

                // Builds the final key: [{prefix}:]{namespace}:{key}
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
                $"Vault AuthMethod '{options.AuthMethod}' not supported. Use 'AppRole' or 'Token'.")
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
                "Vault:RoleId is required for AppRole authentication.");
        if (string.IsNullOrWhiteSpace(options.SecretId))
            throw new InvalidOperationException(
                "Vault:SecretId is required for AppRole authentication.");

        return new AppRoleAuthMethodInfo(options.RoleId, options.SecretId);
    }

    private TokenAuthMethodInfo BuildTokenAuth()
    {
        if (string.IsNullOrWhiteSpace(options.Token))
            throw new InvalidOperationException(
                "Vault:Token is required for Token authentication.");

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
