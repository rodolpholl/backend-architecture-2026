using System.Security.Claims;

namespace FinControl.Infrastructure.Http;

/// <summary>
/// Utility for extracting JWT/Keycloak claims data.
/// Provides generic methods with intelligent fallbacks for common data.
/// </summary>
public static class JwtClaimsExtractor
{
    /// <summary>
    /// Extracts user identification data from JWT.
    /// Tries multiple claims sources for maximum compatibility.
    /// </summary>
    /// <param name="user">Authenticated user principal.</param>
    /// <returns>Tuple with (ID, Name, Email) of the user.</returns>
    /// <exception cref="InvalidOperationException">If 'sub' or 'email' are not found.</exception>
    public static (string Id, string Nome, string Email) ExtractUserData(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            throw new InvalidOperationException("User not authenticated.");

        var id = ExtractClaimWithFallback(
            user,
            "sub",
            ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "Claim 'sub' not found in JWT. Check your Keycloak configuration.");

        var nome = ExtractClaimWithFallback(
            user,
            "name",
            ClaimTypes.Name,
            "preferred_username",
            "given_name")
            ?? "Unknown User";

        var email = ExtractClaimWithFallback(
            user,
            "email",
            ClaimTypes.Email)
            ?? throw new InvalidOperationException(
                "Claim 'email' not found in JWT. Check your Keycloak configuration.");

        return (id, nome, email);
    }

    /// <summary>
    /// Extracts a claim value with fallback to multiple alternatives.
    /// Tries in order: first claim, fallbacks, and returns null if none found.
    /// </summary>
    /// <param name="user">User principal.</param>
    /// <param name="claimNames">Claim names to try, in priority order.</param>
    /// <returns>Claim value or null.</returns>
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
    /// Extracts a specific claim value.
    /// </summary>
    /// <param name="user">User principal.</param>
    /// <param name="claimType">Claim type.</param>
    /// <returns>Claim value or null.</returns>
    public static string? ExtractClaim(ClaimsPrincipal user, string claimType)
        => user?.FindFirst(claimType)?.Value;

    /// <summary>
    /// Extracts a claim value or returns a default value if not found.
    /// </summary>
    /// <param name="user">User principal.</param>
    /// <param name="claimType">Claim type.</param>
    /// <param name="defaultValue">Default value.</param>
    /// <returns>Claim value or default value.</returns>
    public static string ExtractClaimOrDefault(ClaimsPrincipal user, string claimType, string defaultValue)
        => user?.FindFirst(claimType)?.Value ?? defaultValue;
}
