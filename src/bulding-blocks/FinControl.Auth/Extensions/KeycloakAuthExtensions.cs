using FinControl.Infrastructure.Vault;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FinControl.Auth.Extensions;

/// <summary>
/// Configura autenticação JWT Bearer validando tokens emitidos pelo Keycloak.
///
/// O JwtBearer descobre automaticamente as chaves públicas (JWKS) via:
///   {issuer}/.well-known/openid-configuration
///
/// Validações ativas:
///   - Assinatura (chave pública do Keycloak, renovada automaticamente)
///   - Issuer (realm do Keycloak)
///   - Lifetime (exp/nbf do token)
///
/// Audience desabilitado intencionalmente: o claim "aud" do Keycloak varia
/// conforme a configuração do client e pode conter ["account"] em vez do
/// client ID da API. O Kong valida audience upstream via OIDC.
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
            Console.WriteLine("ℹ️  Keycloak issuer não encontrado no Vault — usando default de desenvolvimento");
        }
        else if (string.IsNullOrEmpty(issuer))
        {
            throw new InvalidOperationException(
                $"Secret '{VaultKeys.KeycloakIssuer}' não encontrado no Vault. " +
                "Configuração do Keycloak é obrigatória em produção.");
        }

        var capturedIssuer = issuer;

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                // Dispara discovery automático do OIDC: {issuer}/.well-known/openid-configuration
                // O JwtBearer baixa e renova as chaves JWKS automaticamente.
                opts.Authority = capturedIssuer;

                // Em desenvolvimento, Keycloak roda sem TLS
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
