using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FinControl.Transactions.Core.Domain;
using FinControl.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace FinControl.Transactions.Core.Context;

public class TransactionsDbContext : DbContext
{
    public TransactionsDbContext(DbContextOptions<TransactionsDbContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;


    // <summary>
    /// Applies a specification to a DbSet query and returns the results.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of entities matching the specification.</returns>
    public async Task<List<TEntity>> ListAsync<TEntity>(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        return await ApplySpecificationToQuery(Set<TEntity>(), specification).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Applies a specification to a DbSet query and returns the first result or null.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The first entity matching the specification, or null if none found.</returns>
    public async Task<TEntity?> FirstOrDefaultAsync<TEntity>(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        return await ApplySpecificationToQuery(Set<TEntity>(), specification).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Applies a specification to a DbSet query and counts the matching results.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="specification">The specification to apply.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The count of entities matching the specification.</returns>
    public async Task<int> CountAsync<TEntity>(
        SpecificationDomain<TEntity> specification,
        CancellationToken cancellationToken = default) where TEntity : class
    {
        return await ApplySpecificationToQuery(Set<TEntity>(), specification).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Applies specification criteria, includes, ordering, and paging to a query.
    /// </summary>
    private static IQueryable<TEntity> ApplySpecificationToQuery<TEntity>(
        IQueryable<TEntity> query,
        SpecificationDomain<TEntity> specification) where TEntity : class
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransactionsDbContext).Assembly);

        // Global filter: automatically excludes soft-deleted records from all queries
        modelBuilder.Entity<Transaction>()
            .HasQueryFilter(e => e.DeletedAt == null);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeDateTimeOffsetsToUtc();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        NormalizeDateTimeOffsetsToUtc();
        return base.SaveChanges();
    }

    private void NormalizeDateTimeOffsetsToUtc()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTimeOffset) && property.CurrentValue is DateTimeOffset dto)
                {
                    property.CurrentValue = dto.ToUniversalTime();
                }

                if (property.Metadata.ClrType == typeof(DateTimeOffset?) && property.CurrentValue is DateTimeOffset nullableDto)
                {
                    property.CurrentValue = nullableDto.ToUniversalTime();
                }
            }
        }
    }


}
