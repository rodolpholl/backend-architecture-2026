namespace FinControl.Transactions.Core.Domain;

public sealed class OutboxMessage
{
    public long Id { get; set; }
    public required string MessageType { get; init; }
    public required string Payload { get; init; }
    public required string Exchange { get; init; }
    public required string RoutingKey { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
