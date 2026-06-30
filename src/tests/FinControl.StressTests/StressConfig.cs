namespace FinControl.StressTests;

internal sealed record StressConfig
{
    public string BaseUrl { get; init; } = "http://localhost:8000";
    public string KeycloakUrl { get; init; } = "http://localhost:8081";
    public string Realm { get; init; } = "fincontrol";
    public string ClientId { get; init; } = "fincontrol-backend";
    public string ClientSecret { get; init; } = "fincontrol-backend-secret-12345";
    public string Username { get; init; } = "admin.fincontrol";
    public string Password { get; init; } = "Admin@123456";

    /// <summary>
    /// Duração de cada estágio do teste sustentado (segundos).
    /// Pode ser reduzido para um "smoke test" rápido.
    /// Ex: STRESS_DURATION=15 para validação rápida de 15s
    /// </summary>
    public int SustainedDurationSeconds { get; init; } = 60;

    public static StressConfig FromEnvironment() => new()
    {
        BaseUrl = Env("STRESS_BASE_URL", "http://localhost:8000"),
        KeycloakUrl = Env("STRESS_KEYCLOAK_URL", "http://localhost:8081"),
        Realm = Env("STRESS_REALM", "fincontrol"),
        ClientId = Env("STRESS_CLIENT_ID", "fincontrol-backend"),
        ClientSecret = Env("STRESS_CLIENT_SECRET", "fincontrol-backend-secret-12345"),
        Username = Env("STRESS_USERNAME", "admin.fincontrol"),
        Password = Env("STRESS_PASSWORD", "Admin@123456"),
        SustainedDurationSeconds = int.TryParse(Env("STRESS_DURATION", "60"), out var d) ? d : 60,
    };

    private static string Env(string key, string defaultValue) =>
        Environment.GetEnvironmentVariable(key) ?? defaultValue;
}
