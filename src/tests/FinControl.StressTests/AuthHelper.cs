using System.Text.Json;

namespace FinControl.StressTests;

internal static class AuthHelper
{
    public static async Task<string> FetchTokenAsync(StressConfig config)
    {
        var token = Environment.GetEnvironmentVariable("STRESS_JWT_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("  [auth] JWT carregado via STRESS_JWT_TOKEN");
            return token;
        }

        Console.WriteLine($"  [auth] Obtendo token do Keycloak ({config.KeycloakUrl}/realms/{config.Realm})...");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        var url = $"{config.KeycloakUrl}/realms/{config.Realm}/protocol/openid-connect/token";

        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "password",
            ["client_id"]     = config.ClientId,
            ["client_secret"] = config.ClientSecret,
            ["username"]      = config.Username,
            ["password"]      = config.Password,
            ["scope"]         = "openid profile email",
        };

        using var response = await http.PostAsync(url, new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Keycloak retornou HTTP {(int)response.StatusCode}. " +
                $"Verifique se o docker-compose esta no ar e as credenciais estao corretas. " +
                $"Resposta: {body}");

        using var doc = JsonDocument.Parse(body);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("'access_token' ausente na resposta do Keycloak.");

        Console.WriteLine("  [auth] Token obtido com sucesso.");
        return accessToken;
    }
}
