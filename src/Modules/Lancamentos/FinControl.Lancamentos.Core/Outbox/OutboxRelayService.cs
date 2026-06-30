using FinControl.Infrastructure.Messaging;
using FinControl.Transactions.Core.Context;
using FinControl.Transactions.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace FinControl.Transactions.Core.Outbox;

public sealed class OutboxRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly ResiliencePipeline _pipeline;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    public OutboxRelayService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pipeline = BuildPipeline(logger);
    }

    private static ResiliencePipeline BuildPipeline(ILogger logger) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Outbox: retry {Attempt}/3 para mensagem — {Exception}",
                        args.AttemptNumber + 1,
                        args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "OutboxRelayService iniciado — polling a cada {Interval}s, batch={Batch}.",
                PollingInterval.TotalSeconds, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox: erro inesperado no ciclo de relay.");
            }

            await Task.Delay(PollingInterval, stoppingToken)
                      .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _logger.LogInformation("OutboxRelayService encerrado.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.DeliveredAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Outbox: {Count} mensagem(ns) pendente(s).", pending.Count);

        foreach (var message in pending)
            await DeliverAsync(db, publisher, message, ct);
    }

    private async Task DeliverAsync(
        TransactionsDbContext db,
        IRabbitMqPublisher publisher,
        OutboxMessage message,
        CancellationToken ct)
    {
        try
        {
            await _pipeline.ExecuteAsync(
                async token => await publisher.PublishRawAsync(
                    message.Payload, message.Exchange, message.RoutingKey, token),
                ct);

            message.DeliveredAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "Outbox: mensagem {Id} ({Type}) entregue | Exchange={Exchange} RoutingKey={RoutingKey}",
                    message.Id, message.MessageType, message.Exchange, message.RoutingKey);
        }
        catch (Exception ex)
        {
            message.RetryCount++;
            message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await db.SaveChangesAsync(ct);

            _logger.LogError(ex,
                "Outbox: falha definitiva na mensagem {Id} ({Type}). RetryCount={RetryCount}",
                message.Id, message.MessageType, message.RetryCount);
        }
    }
}
