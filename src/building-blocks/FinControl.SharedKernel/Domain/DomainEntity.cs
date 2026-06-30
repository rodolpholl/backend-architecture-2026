namespace FinControl.SharedKernel.Domain;

public abstract class DomainEntity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    public Guid? NavigationId { get; set; }

    protected DomainEntity() { }

    protected DomainEntity(TId id) => Id = id;
    protected DomainEntity(Guid navigationId) => NavigationId = navigationId;

    public override bool Equals(object? obj)
    {
        if (obj is not DomainEntity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(DomainEntity<TId>? left, DomainEntity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(DomainEntity<TId>? left, DomainEntity<TId>? right) => !(left == right);
}
