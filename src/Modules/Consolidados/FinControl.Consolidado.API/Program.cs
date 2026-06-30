using FinControl.Auth.Extensions;
using FinControl.Consolidated.API.Configuration;
using FinControl.Infrastructure.Extensions;
using FinControl.Infrastructure.Middleware;
using FinControl.Infrastructure.Vault;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ==================== SECRETS CONFIGURATION (VAULT) ====================
try
{
    builder.AddFinControlVault();
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Vault not available in development: {ex.Message}");
}

// ==================== LOGGING ====================
try
{
    builder.AddFinControlSerilog("fincontrol-consolidados");
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Serilog not fully configured in development: {ex.Message}");
}

// ==================== OBSERVABILITY ====================
try
{
    builder.AddFinControlObservability("fincontrol-consolidados");
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Observability not available in development: {ex.Message}");
}

// ==================== HEALTH CHECKS ====================
try
{
    builder.Services.AddFinControlHealthChecks(
        builder.Configuration,
        includeRedis: true,
        includeRabbitMq: !builder.Environment.IsDevelopment());
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Health checks incomplete in development: {ex.Message}");
}

// ==================== JWT AUTHENTICATION (KEYCLOAK) ====================
try
{
    builder.AddFinControlKeycloakAuth();
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"⚠️  Keycloak not configured in development: {ex.Message}");
}

// ==================== API / OPENAPI ====================
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// ==================== ALL FEATURE MODULES ====================
builder.AddAllModules();

// ==================== EXCEPTION HANDLING ====================
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ==================== BUILD PIPELINE HTTP ====================
var app = builder.Build();

// ==================== MIDDLEWARES HTTP ====================

// 1. CorrelationId — MUST BE FIRST: populates HttpContext.Items["X-Correlation-Id"]
//    used by SubscriptionKeyMiddleware, GlobalExceptionHandler and SerilogRequestLogging
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Request Logging (Serilog) — after CorrelationId to capture the ID in logs
try
{
    app.UseFinControlRequestLogging();
}
catch
{
    // If Serilog wasn't configured, continue without it
}

// 2. HTTPS Redirect
app.UseHttpsRedirection();

// 3. Observability
try
{
    app.UseFinControlObservability();
}
catch
{
    // If observability wasn't configured, continue without it
}

// 4. Global Exception Handler (RFC 7807 ProblemDetails)
app.UseExceptionHandler();

// 5. Subscription Key (second layer after Kong — covers requests that bypass the gateway)
app.UseSubscriptionKeyValidation(VaultKeys.KongConsolidadosSubscriptionKey);

// 6. Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// ==================== HEALTH CHECKS ====================
// /health      → liveness  (no checks, just confirms the process is alive)
// /health/ready → readiness (executes checks with "ready" tag: postgres, redis, rabbitmq)
app.MapFinControlHealthChecks();

// ==================== ENDPOINTS ====================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    });
}

app.MapAllModules();

// Prometheus /metrics
app.MapFinControlMetricsEndpoint();

// ==================== RUN ====================
Console.WriteLine($"\n🚀 Consolidated starting in mode: {(app.Environment.IsDevelopment() ? "DEVELOPMENT" : "PRODUCTION")}\n");
app.Run();
