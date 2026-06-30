using System.Security.Claims;

namespace FinControl.Infrastructure.Http;

/// <summary>
/// Utilitário para extrair dados de claims do JWT/Keycloak.
/// Fornece métodos genéricos e com fallbacks inteligentes para dados comuns.
/// </summary>
public static class JwtClaimsExtractor
{
    /// <summary>
    /// Extrai dados de identificação do usuário do JWT.
    /// Tenta múltiplas fontes de claims para máxima compatibilidade.
    /// </summary>
    /// <param name="user">Principal do usuário autenticado.</param>
    /// <returns>Tupla com (ID, Nome, Email) do usuário.</returns>
    /// <exception cref="InvalidOperationException">Se 'sub' ou 'email' não forem encontrados.</exception>
    public static (string Id, string Nome, string Email) ExtractUserData(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("Usuário não autenticado.");

        var id = ExtractClaimWithFallback(
            user,
            "sub",
            ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "Claim 'sub' não encontrada no JWT. Verifique a configuração do Keycloak.");

        var nome = ExtractClaimWithFallback(
            user,
            "name",
            ClaimTypes.Name,
            "preferred_username",
            "given_name")
            ?? "Usuário Desconhecido";

        var email = ExtractClaimWithFallback(
            user,
            "email",
            ClaimTypes.Email)
            ?? throw new InvalidOperationException(
                "Claim 'email' não encontrada no JWT. Verifique a configuração do Keycloak.");

        return (id, nome, email);
    }

    /// <summary>
    /// Extrai um valor de claim com fallback para múltiplas alternativas.
    /// Tenta na ordem: primeira claim, fallbacks, e retorna null se nenhuma encontrada.
    /// </summary>
    /// <param name="user">Principal do usuário.</param>
    /// <param name="claimNames">Nome das claims a tentar, em ordem de prioridade.</param>
    /// <returns>Amount da claim ou null.</returns>
    public static string? ExtractClaimWithFallback(ClaimsPrincipal user, params string[] claimNames)
    {
        if (user == null || claimNames.Length == 0)
            return null;

        foreach (var claimName in claimNames)
        {
            var value = user.FindFirst(claimName)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Extrai um valor de claim específico.
    /// </summary>
    /// <param name="user">Principal do usuário.</param>
    /// <param name="claimType">Type de claim.</param>
    /// <returns>Amount da claim ou null.</returns>
    public static string? ExtractClaim(ClaimsPrincipal user, string claimType)
        => user?.FindFirst(claimType)?.Value;

    /// <summary>
    /// Extrai um valor de claim ou retorna um valor padrão se não encontrado.
    /// </summary>
    /// <param name="user">Principal do usuário.</param>
    /// <param name="claimType">Type de claim.</param>
    /// <param name="defaultValue">Amount padrão.</param>
    /// <returns>Amount da claim ou valor padrão.</returns>
    public static string ExtractClaimOrDefault(ClaimsPrincipal user, string claimType, string defaultValue)
        => user?.FindFirst(claimType)?.Value ?? defaultValue;
}
