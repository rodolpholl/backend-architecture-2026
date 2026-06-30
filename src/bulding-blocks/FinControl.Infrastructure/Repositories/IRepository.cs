using FinControl.SharedKernel.Domain;

namespace FinControl.Infrastructure.Repositories;

/// <summary>
/// Generic repository interface for specification-based queries.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets a list of entities matching the specification.
    /// </summary>
    Task<List<TEntity>> ListAsync(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity matching the specification or null.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts entities matching the specification.
    /// </summary>
    Task<int> CountAsync(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an entity to the repository.
    /// </summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an entity in the repository.
    /// </summary>
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entity from the repository.
    /// </summary>
    Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
