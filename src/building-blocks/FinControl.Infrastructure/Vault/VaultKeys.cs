namespace FinControl.Infrastructure.Vault;

/// <summary>
/// Mapa completo de chaves do HashiCorp Vault para o sistema FinControl,
/// alinhado à estrutura real do Vault (mount: <c>secret</c>, prefixo: <c>dev/</c>).
///
/// ────────────────────────────────────────────────────────────────────────────
/// ESTRUTURA REAL DO VAULT (KV v2)
/// ────────────────────────────────────────────────────────────────────────────
///
///   secrets > secret > dev/
///   ├── dev/grafana      → loki_url, otlp_endpoint, prometheus_pushgateway
///   ├── dev/keycloak     → realm, url, issuer, jwks_uri, kong_client_id, kong_client_secret,
///   │                       api_client_id, api_client_secret
///   ├── dev/postgres     → connection_string
///   ├── dev/rabbitmq     → uri
///   ├── dev/redis        → connection_string
///   └── dev/vault        → (metadados internos / AppRole credentials)
///
/// ────────────────────────────────────────────────────────────────────────────
/// COMO AS CHAVES SÃO INJETADAS NO IConfiguration
/// ────────────────────────────────────────────────────────────────────────────
///
/// O VaultConfigurationProvider usa o ÚLTIMO SEGMENTO do path como namespace:
///
///   Path "dev/postgres" + key "connection_string"
///   → IConfiguration["postgres:connection_string"]
///
///   Path "dev/grafana" + key "loki_url"
///   → IConfiguration["grafana:loki_url"]
///
/// Acesse sempre via: builder.Configuration[VaultKeys.CONSTANTE]
///
/// ────────────────────────────────────────────────────────────────────────────
/// VAULT SETTINGS (vault.settings.json)
/// ────────────────────────────────────────────────────────────────────────────
/// {
///   "Vault": {
///     "MountPoint": "secret",
///     "SecretPaths": ["dev/postgres", "dev/redis", "dev/rabbitmq", "dev/grafana", "dev/vault"],
///     "ConfigurationPrefix": ""
///   }
/// }
///
/// ────────────────────────────────────────────────────────────────────────────
/// SETUP VAULT CLI (ambiente dev)
/// ────────────────────────────────────────────────────────────────────────────
///   vault kv put secret/dev/postgres   connection_string="Host=postgres;Database=fincontrol_lancamentos;Username=fincontrol_admin;Password=..."
///   vault kv put secret/dev/redis      connection_string="redis:6379,password=...,abortConnect=false"
///   vault kv put secret/dev/rabbitmq   uri="amqp://user:pass@rabbitmq:5672/fincontrol"
///   vault kv put secret/dev/grafana    loki_url="http://loki:3100" otlp_endpoint="http://jaeger:4317" prometheus_pushgateway="http://prometheus:9091"
///   vault kv put secret/dev/keycloak   realm="fincontrol" url="http://keycloak:8080" issuer="..." kong_client_id="kong-client" kong_client_secret="..." api_client_id="fincontrol-api" api_client_secret="..."
///   vault kv put secret/dev/vault      role_id="..." secret_id="..."
/// </summary>
public static class VaultKeys
{
    // ── dev/postgres ─────────────────────────────────────────────────────────

    /// <summary>
    /// Connection string do PostgreSQL principal.
    /// Vault path: <c>dev/postgres</c> → key <c>connection_string</c>
    /// IConfiguration: <c>postgres:connection_string</c>
    /// </summary>
    public const string PostgresConnection = "postgres:connection_string";

    // ── dev/redis ────────────────────────────────────────────────────────────

    /// <summary>
    /// Connection string do Redis (cache distribuído e sessões).
    /// Vault path: <c>dev/redis</c> → key <c>connection_string</c>
    /// IConfiguration: <c>redis:connection_string</c>
    /// </summary>
    public const string RedisConnection = "redis:connection_string";

    // ── dev/rabbitmq ─────────────────────────────────────────────────────────

    /// <summary>
    /// URI de conexão do RabbitMQ. Formato: <c>amqp://user:pass@host:5672/vhost</c>
    /// Vault path: <c>dev/rabbitmq</c> → key <c>uri</c>
    /// IConfiguration: <c>rabbitmq:uri</c>
    /// </summary>
    public const string RabbitMqUri = "rabbitmq:uri";

    // ── dev/grafana ──────────────────────────────────────────────────────────

    /// <summary>
    /// URL do Grafana Loki para push de logs estruturados via Serilog.
    /// Ex: <c>http://loki:3100</c>
    /// Vault path: <c>dev/grafana</c> → key <c>loki_url</c>
    /// IConfiguration: <c>grafana:loki_url</c>
    /// </summary>
    public const string LokiUrl = "grafana:loki_url";

    /// <summary>
    /// Endpoint OTLP gRPC para envio de traces (Grafana Tempo / Jaeger).
    /// Ex: <c>http://tempo:4317</c>
    /// Vault path: <c>dev/grafana</c> → key <c>otlp_endpoint</c>
    /// IConfiguration: <c>grafana:otlp_endpoint</c>
    /// </summary>
    public const string OtlpEndpoint = "grafana:otlp_endpoint";

    /// <summary>
    /// URL do Prometheus Pushgateway (para métricas de workers/jobs).
    /// Ex: <c>http://pushgateway:9091</c>
    /// Vault path: <c>dev/grafana</c> → key <c>prometheus_pushgateway</c>
    /// IConfiguration: <c>grafana:prometheus_pushgateway</c>
    /// </summary>
    public const string PrometheusPushgateway = "grafana:prometheus_pushgateway";

    // ── dev/vault ────────────────────────────────────────────────────────────
    // O path dev/vault contém as credenciais AppRole para renovação automática
    // de tokens em produção. Não exposto como constante pública — lido apenas
    // internamente pelo VaultConfigurationProvider durante o bootstrap.

    // ── dev/keycloak ─────────────────────────────────────────────────────────

    /// <summary>
    /// Nome do realm Keycloak.
    /// Vault path: <c>dev/keycloak</c> → key <c>realm</c>
    /// IConfiguration: <c>keycloak:realm</c>
    /// </summary>
    public const string KeycloakRealm = "keycloak:realm";

    /// <summary>
    /// URL base do Keycloak. Ex: <c>http://keycloak:8080</c>
    /// Vault path: <c>dev/keycloak</c> → key <c>url</c>
    /// IConfiguration: <c>keycloak:url</c>
    /// </summary>
    public const string KeycloakUrl = "keycloak:url";

    /// <summary>
    /// Issuer OIDC do realm. Ex: <c>http://keycloak:8080/realms/fincontrol</c>
    /// Vault path: <c>dev/keycloak</c> → key <c>issuer</c>
    /// IConfiguration: <c>keycloak:issuer</c>
    /// </summary>
    public const string KeycloakIssuer = "keycloak:issuer";

    /// <summary>
    /// Endpoint JWKS para validação de tokens JWT.
    /// Vault path: <c>dev/keycloak</c> → key <c>jwks_uri</c>
    /// IConfiguration: <c>keycloak:jwks_uri</c>
    /// </summary>
    public const string KeycloakJwksUri = "keycloak:jwks_uri";

    /// <summary>
    /// Client ID do Kong para OIDC.
    /// Vault path: <c>dev/keycloak</c> → key <c>kong_client_id</c>
    /// IConfiguration: <c>keycloak:kong_client_id</c>
    /// </summary>
    public const string KeycloakKongClientId = "keycloak:kong_client_id";

    /// <summary>
    /// Client Secret do Kong para OIDC.
    /// Vault path: <c>dev/keycloak</c> → key <c>kong_client_secret</c>
    /// IConfiguration: <c>keycloak:kong_client_secret</c>
    /// </summary>
    public const string KeycloakKongClientSecret = "keycloak:kong_client_secret";

    /// <summary>
    /// Client ID da API FinControl (Swagger / M2M / testes).
    /// Vault path: <c>dev/keycloak</c> → key <c>api_client_id</c>
    /// IConfiguration: <c>keycloak:api_client_id</c>
    /// </summary>
    public const string KeycloakApiClientId = "keycloak:api_client_id";

    /// <summary>
    /// Client Secret da API FinControl.
    /// Vault path: <c>dev/keycloak</c> → key <c>api_client_secret</c>
    /// IConfiguration: <c>keycloak:api_client_secret</c>
    /// </summary>
    public const string KeycloakApiClientSecret = "keycloak:api_client_secret";

    // ── dev/kong ─────────────────────────────────────────────────────────────
    // Subscription keys usadas pelo Kong key-auth plugin.
    // Enviadas pelos clientes no header: X-Subscription-Key
    // Lidas pelo kong-init.sh para provisionar consumers + credentials no Kong.

    /// <summary>
    /// Subscription key do Kong para a API de Lançamentos.
    /// Vault path: <c>dev/kong</c> → key <c>lancamentos_subscription_key</c>
    /// IConfiguration: <c>kong:lancamentos_subscription_key</c>
    /// Header HTTP: <c>X-Subscription-Key</c>
    /// </summary>
    public const string KongTransactionsSubscriptionKey = "kong:lancamentos_subscription_key";

    /// <summary>
    /// Subscription key do Kong para a API de Consolidados.
    /// Vault path: <c>dev/kong</c> → key <c>consolidados_subscription_key</c>
    /// IConfiguration: <c>kong:consolidados_subscription_key</c>
    /// Header HTTP: <c>X-Subscription-Key</c>
    /// </summary>
    public const string KongConsolidadosSubscriptionKey = "kong:consolidados_subscription_key";
}

