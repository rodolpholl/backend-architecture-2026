using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FinControl.Entries.API.Configuration;

/// <summary>
/// Adiciona o esquema de segurança Bearer JWT ao documento OpenAPI,
/// habilitando autenticação pelo botão "Authorize" no Scalar UI.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JWT",
            Description =
                "Token JWT do Keycloak.\n\n" +
                "POST http://localhost:8081/realms/fincontrol/protocol/openid-connect/token\n" +
                "Body: grant_type=password&client_id=fincontrol-api&username=dev.user&password=Dev@123456!"
        };
    }
}

