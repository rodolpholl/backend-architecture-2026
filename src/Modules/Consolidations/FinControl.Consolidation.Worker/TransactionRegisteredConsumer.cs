using System.Text;
using System.Text.Json;
using FinControl.Consolidation.Core.Features.Commands.UpdateConsolidatedBalance;
using FinControl.Infrastructure.Vault;
using FinControl.SharedKernel.Domain.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinControl.Consolidation.Worker;

public sealed class TransactionRegisteredConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<TransactionRegisteredConsumer> logger)
    : BackgroundService
{
    private const string Exchange = "transactions.events";
    private const string Queue = "FinControl.Consolidation.transaction-registered";
    private const string RoutingKey = "transaction.created";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitMqUri = configuration[VaultKeys.RabbitMqUri];

        if (string.IsNullOrEmpty(rabbitMqUri))
        {
            logger.LogWarning("RabbitMQ not configured — TransactionRegisteredConsumer inactive.");
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            return;
        }

        var delay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConsumerLoopAsync(rabbitMqUri, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "RabbitMQ connection lost. Reconnecting in {DelaySeconds}s...",
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
        }
    }

    private async Task RunConsumerLoopAsync(string rabbitMqUri, CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqUri) };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic,
            durable: true, autoDelete: false, cancellationToken: ct);

        // x-dead-letter-exchange must match the value that Wolverine (Transactions API)
        // originally declared for the queue — without this argument RabbitMQ rejects with PRECONDITION_FAILED.
        var queueArgs = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "wolverine-dead-letter-queue" };
        await channel.QueueDeclareAsync(Queue,
            durable: true, exclusive: false, autoDelete: false,
            arguments: queueArgs, cancellationToken: ct);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: ct);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) => HandleMessageAsync(channel, args, ct);

        await channel.BasicConsumeAsync(Queue, autoAck: false, consumer, ct);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(
                "TransactionRegisteredConsumer active | Queue={Queue} Exchange={Exchange} RoutingKey={RoutingKey}",
                Queue, Exchange, RoutingKey);

        await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken ct)
    {
        TransactionRegisteredMessage? message = null;
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.Span);
            message = JsonSerializer.Deserialize<TransactionRegisteredMessage>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to deserialize message. DeliveryTag={DeliveryTag}",
                args.DeliveryTag);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        if (message is null)
        {
            logger.LogWarning("Message deserialized as null. DeliveryTag={DeliveryTag}", args.DeliveryTag);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, CancellationToken.None);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetService<UpdateConsolidatedBalanceCommandHandler>();
            if (handler is null)
            {
                logger.LogWarning(
                    "UpdateConsolidatedBalanceCommandHandler not registered (Redis unavailable). Discarding message Id={Id}.",
                    message.Id);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, CancellationToken.None);
                return;
            }

            await handler.Handle(new UpdateConsolidatedBalanceCommand(message.Amount, message.TransactionDate), ct);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing TransactionRegistered. Id={Id} Amount={Amount} DeliveryTag={DeliveryTag}",
                message.Id, message.Amount, args.DeliveryTag);
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
        }
    }
}

