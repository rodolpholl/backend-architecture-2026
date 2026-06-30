namespace FinControl.Infrastructure.Vault;

/// <summary>
/// Configurações do HashiCorp Vault lidas de appsettings / variáveis de ambiente.
/// Seção esperada: "Vault"
/// </summary>
public sealed class VaultOptions
{
    public const string SectionName = "Vault";

    /// <summary>Endereço do servidor Vault. Ex: https://vault.empresa.com:8200</summary>
    public string Address { get; init; } = "http://localhost:8200";

    /// <summary>
    /// Método de autenticação: "AppRole" (produção) | "Token" (dev local).
    /// </summary>
    public string AuthMethod { get; init; } = "AppRole";

    // ── AppRole (produção) ──────────────────────────────────────────────────
    /// <summary>RoleId gerado pelo Vault para a aplicação (AppRole).</summary>
    public string? RoleId { get; init; }

    /// <summary>
    /// SecretId gerado pelo Vault para a aplicação (AppRole).
    /// Em produção, vem de variável de ambiente ou volume Kubernetes — NUNCA commite aqui.
    /// </summary>
    public string? SecretId { get; init; }

    // ── Token (desenvolvimento local) ───────────────────────────────────────
    /// <summary>Token raiz para uso exclusivo em desenvolvimento local.</summary>
    public string? Token { get; init; }

    // ── Paths dos secrets ───────────────────────────────────────────────────
    /// <summary>
    /// Lista de paths KV (v2) cujos valores serão injetados no IConfiguration.
    /// Ex: ["fincontrol/lancamentos", "fincontrol/shared"]
    /// </summary>
    public string[] SecretPaths { get; init; } = [];

    /// <summary>Mount point do KV secrets engine. Padrão do Vault: "secret".</summary>
    public string MountPoint { get; init; } = "secret";

    /// <summary>
    /// Prefixo adicionado às chaves injetadas no IConfiguration.
    /// Deixe vazio ("") para injetar os secrets direto na raiz do IConfiguration,
    /// permitindo acesso transparente via chaves padrão (ex: "ConnectionStrings:DefaultConnection").
    /// </summary>
    public string ConfigurationPrefix { get; init; } = "";

    /// <summary>Se true, falhas na leitura do Vault lançam exceção na inicialização.</summary>
    public bool Required { get; init; } = true;
}
