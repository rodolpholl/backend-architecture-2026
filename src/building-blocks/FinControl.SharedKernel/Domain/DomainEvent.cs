namespace FinControl.SharedKernel.Domain;

public abstract record DomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType => GetType().Name;
}
