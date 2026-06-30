namespace FinControl.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync<T>(T message, string exchange, string routingKey, CancellationToken ct = default);

    Task PublishRawAsync(string payload, string exchange, string routingKey, CancellationToken ct = default);
}
