using System.Text.Json;
using FinControl.Infrastructure.Vault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FinControl.Infrastructure.Messaging;

/// <summary>
/// Publishes messages directly to a RabbitMQ exchange via AMQP.
/// Maintains a single reusable IConnection (thread-safe) and creates an IChannel per publish.
/// </summary>
public sealed class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly string? _rabbitMqUri;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _rabbitMqUri = configuration[VaultKeys.RabbitMqUri];
        _logger = logger;
    }

    public async Task PublishAsync<T>(
        T message,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_rabbitMqUri))
        {
            _logger.LogWarning(
                "RabbitMQ not configured — message {MessageType} discarded (exchange={Exchange} routingKey={RoutingKey}).",
                typeof(T).Name, exchange, routingKey);
            return;
        }

        var connection = await GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var props = new BasicProperties { ContentType = "application/json", Persistent = true };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Message {MessageType} published | Exchange={Exchange} RoutingKey={RoutingKey}",
                typeof(T).Name, exchange, routingKey);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory { Uri = new Uri(_rabbitMqUri!) };
            _connection = await factory.CreateConnectionAsync(ct);
            _logger.LogInformation("RabbitMqPublisher: new connection established.");
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PublishRawAsync(
        string payload,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_rabbitMqUri))
        {
            _logger.LogWarning(
                "RabbitMQ not configured — payload discarded (exchange={Exchange} routingKey={RoutingKey}).",
                exchange, routingKey);
            return;
        }

        var connection = await GetConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var body = System.Text.Encoding.UTF8.GetBytes(payload);
        var props = new BasicProperties { ContentType = "application/json", Persistent = true };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation(
                "Payload published | Exchange={Exchange} RoutingKey={RoutingKey}",
                exchange, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        _lock.Dispose();
    }
}
