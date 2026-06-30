using FinControl.Consolidation.Core.Features.Commands.UpdateConsolidatedBalance;
using FinControl.Consolidation.Worker;
using FinControl.Infrastructure.Cache;
using FinControl.Infrastructure.Vault;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("vault.settings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"vault.settings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false);

// ==================== VAULT ====================
try
{
    var vaultOptions = builder.Configuration
        .GetSection(VaultOptions.SectionName)
        .Get<VaultOptions>() ?? new VaultOptions();
    builder.Configuration.AddVaultSecrets(vaultOptions);
}
catch (Exception ex) when (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"Vault nao disponivel em desenvolvimento: {ex.Message}");
}

// ==================== REDIS ====================
var redisConnection = builder.Configuration[VaultKeys.RedisConnection];

if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "FinControl:";
    });
    builder.Services.AddSingleton<IConnectionMultiplexer>(
        _ => ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddSingleton<RedisCacheService>();
    builder.Services.AddSingleton<IRedisLockService, RedisLockService>();
    builder.Services.AddScoped<UpdateConsolidatedBalanceCommandHandler>();
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"Secret '{VaultKeys.RedisConnection}' nao encontrado. Redis e obrigatorio em producao.");
}
else
{
    Console.WriteLine("Redis nao configurado - cache indisponivel em desenvolvimento");
}

// ==================== CONSUMER ====================
builder.Services.AddHostedService<TransactionRegisteredConsumer>();

var host = builder.Build();

Console.WriteLine($"\nConsolidado Worker iniciando em modo: {(builder.Environment.IsDevelopment() ? "DESENVOLVIMENTO" : "PRODUCAO")}\n");

host.Run();

