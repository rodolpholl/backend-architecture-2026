using FinControl.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinControl.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation for specification-based queries.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public abstract class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly DbContext _context;

    public Repository(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets a list of entities matching the specification.
    /// </summary>
    public async Task<List<TEntity>> ListAsync(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecificationToQuery(_context.Set<TEntity>(), specification)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets the first entity matching the specification or null.
    /// </summary>
    public async Task<TEntity?> FirstOrDefaultAsync(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecificationToQuery(_context.Set<TEntity>(), specification)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Counts entities matching the specification.
    /// </summary>
    public async Task<int> CountAsync(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecificationToQuery(_context.Set<TEntity>(), specification)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Adds an entity to the repository.
    /// </summary>
    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Updates an entity in the repository.
    /// </summary>
    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Set<TEntity>().Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes an entity from the repository.
    /// </summary>
    public Task RemoveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _context.Set<TEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves all pending changes to the database.
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies specification criteria, includes, ordering, and paging to a query.
    /// </summary>
    private static IQueryable<TEntity> ApplySpecificationToQuery(
        IQueryable<TEntity> query,
        SpecificationDomain<TEntity> specification)
    {
        var result = query;

        // Apply all criteria predicates
        foreach (var criterion in specification.Criteria)
        {
            result = result.Where(criterion);
        }

        // Apply includes for eager loading
        foreach (var includeString in specification.IncludeStrings)
        {
            result = result.Include(includeString);
        }

        // Apply ordering
        foreach (var (orderExpression, isDescending) in specification.OrderExpressions)
        {
            result = isDescending
                ? result.OrderByDescending(orderExpression)
                : result.OrderBy(orderExpression);
        }

        // Apply paging
        if (specification.IsPagingEnabled)
        {
            var skip = (specification.PageIndex - 1) * specification.PageSize;
            result = result.Skip(skip).Take(specification.PageSize);
        }

        // Apply tracking disable if specified
        if (specification.IsTrackingDisabled)
        {
            result = result.AsNoTracking();
        }

        return result;
    }
}
