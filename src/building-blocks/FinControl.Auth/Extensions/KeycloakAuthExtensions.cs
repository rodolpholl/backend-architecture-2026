using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FinControl.Auth.Extensions;

/// <summary>
/// Configures JWT Bearer authentication validating tokens issued by Keycloak.
///
/// JwtBearer automatically discovers public keys (JWKS) via:
///   {issuer}/.well-known/openid-configuration
///
/// Active validations:
///   - Signature (Keycloak public key, automatically renewed)
///   - Issuer (Keycloak realm)
///   - Lifetime (exp/nbf of the token)
///
/// Audience is intentionally disabled: the Keycloak "aud" claim varies
/// depending on client configuration and may contain ["account"] instead of
/// the API client ID. Kong validates audience upstream via OIDC.
/// </summary>
public static class KeycloakAuthExtensions
{
    public static WebApplicationBuilder AddFinControlKeycloakAuth(
        this WebApplicationBuilder builder)
    {
        var issuer = builder.Configuration[VaultKeys.KeycloakIssuer];

        if (string.IsNullOrEmpty(issuer) && builder.Environment.IsDevelopment())
        {
            issuer = "http://localhost:8081/realms/fincontrol";
            Console.WriteLine("ℹ️  Keycloak issuer not found in Vault — using development default");
        }
        else if (string.IsNullOrEmpty(issuer))
        {
            throw new InvalidOperationException(
                $"Secret '{VaultKeys.KeycloakIssuer}' not found in Vault. " +
                "Keycloak configuration is required in production.");
        }

        var capturedIssuer = issuer;

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                // Triggers automatic OIDC discovery: {issuer}/.well-known/openid-configuration
                // JwtBearer downloads and automatically renews JWKS keys.
                opts.Authority = capturedIssuer;

                // In development, Keycloak runs without TLS
                opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = capturedIssuer,
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }
}
