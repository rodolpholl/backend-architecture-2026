using FinControl.Auth.Extensions;
using FinControl.Infrastructure.Extensions;
using FinControl.Infrastructure.Middleware;
using FinControl.Infrastructure.Vault;
using FinControl.Transactions.API.Configuration;
using FinControl.Transactions.Core.Context;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ==================== CONFIGURAÇÃO DE SECRETS (VAULT) ====================
// ⚠️ DEVE SER PRIMEIRO: inicia o pipeline de leitura de secrets do Vault
try
{
    builder.AddFinControlVault();
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Vault não disponível em desenvolvimento: {ex.Message}");
}

// ==================== LOGGING ====================
try
{
    builder.AddFinControlSerilog("fincontrol-lancamentos");
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Serilog não fully configurado em desenvolvimento: {ex.Message}");
}

// ==================== OBSERVABILIDADE ====================
try
{
    builder.AddFinControlObservability("fincontrol-lancamentos");
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Observabilidade não disponível em desenvolvimento: {ex.Message}");
}

// ==================== DATABASE ====================
// PostgreSQL + EF Core + Outbox (Wolverine)
builder.Services.AddDbContext<TransactionsDbContext>(opts =>
{
    var connectionString = builder.Configuration[VaultKeys.PostgresConnection];
   
    if (string.IsNullOrEmpty(connectionString))
        throw new InvalidOperationException(
            $"Secret '{VaultKeys.PostgresConnection}' não encontrado no Vault (dev/postgres → connection_string).");
    

    opts.UseNpgsql(connectionString);
});

// ==================== HEALTH CHECKS ====================
try
{
    builder.Services.AddFinControlHealthChecks(
        builder.Configuration,
        includeRedis: !builder.Environment.IsDevelopment(),
        includeRabbitMq: !builder.Environment.IsDevelopment());
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Health checks incompletos em desenvolvimento: {ex.Message}");
}

// ==================== AUTENTICAÇÃO JWT (KEYCLOAK) ====================
try
{
    builder.AddFinControlKeycloakAuth();
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Keycloak não configurado em desenvolvimento: {ex.Message}");
}

// ==================== API / OPENAPI ====================
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// ==================== TODOS OS MÓDULOS DE FEATURES ====================
// Registra Wolverine, handlers, validators, endpoints para todos os módulos
builder.AddAllModules();

// ==================== EXCEPTION HANDLING ====================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ==================== BUILD PIPELINE HTTP ====================

var app = builder.Build();

// ==================== DATABASE MIGRATIONS ====================
// Aplica automaticamente todas as migrations pendentes no startup.
// Falha imediatamente em qualquer ambiente — migrations pendentes indicam
// que o banco está em estado inconsistente com o código em execução.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count > 0)
    {
        Console.WriteLine($"Aplicando {pending.Count} migration(s) pendente(s): {string.Join(", ", pending)}");
        await db.Database.MigrateAsync();
        Console.WriteLine("Migrations aplicadas com sucesso.");
    }
}

// ==================== MIDDLEWARES HTTP ====================

// 1. CorrelationId — DEVE SER PRIMEIRO: popula HttpContext.Items["X-Correlation-Id"]
//    usado por SubscriptionKeyMiddleware, GlobalExceptionHandler e SerilogRequestLogging
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Request Logging (Serilog) — após CorrelationId para capturar o ID nos logs
try
{
    app.UseFinControlRequestLogging();
}
catch
{
    // Se Serilog não foi configurado, continua sem
}

// 2. HTTPS Redirect
app.UseHttpsRedirection();

// 3. Observabilidade (OpenTelemetry Traces + Prometheus Metrics)
try
{
    app.UseFinControlObservability();
}
catch
{
    // Se observabilidade não foi configurada, continua sem
}

// 4. Exception Handler Global (RFC 7807 ProblemDetails)
app.UseExceptionHandler();

// 5. Subscription Key (segunda camada após Kong — cobre requisições que bypassam o gateway)
app.UseSubscriptionKeyValidation(VaultKeys.KongTransactionsSubscriptionKey);

// 6. Autenticação e Autorização (DEVE ser antes de MapAllModules)
app.UseAuthentication();
app.UseAuthorization();

// ==================== HEALTH CHECKS ====================
// /health      → liveness  (sem checks, só confirma que o processo está vivo)
// /health/ready → readiness (executa checks com tag "ready": postgres, redis, rabbitmq)
app.MapFinControlHealthChecks();

// ==================== ENDPOINTS ====================

// Development: OpenAPI + Scalar UI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

// Endpoints de todos os módulos (descobertos automaticamente pelo Wolverine)
app.MapAllModules();

// Prometheus /metrics
app.MapFinControlMetricsEndpoint();

// ==================== RUN ====================
Console.WriteLine($"\n🚀 Aplicação iniciando em modo: {(app.Environment.IsDevelopment() ? "DESENVOLVIMENTO" : "PRODUÇÃO")}\n");
app.Run();

