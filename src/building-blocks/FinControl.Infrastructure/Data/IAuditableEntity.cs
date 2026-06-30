namespace FinControl.Infrastructure.Data;

/// <summary>
/// Entidades que implementam esta interface recebem preenchimento
/// automático de CreatedAt e UpdatedAt no BaseDbContext.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
}
