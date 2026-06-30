using FinControl.Infrastructure.Repositories;
using FinControl.Transactions.Core.Context;

namespace FinControl.Transactions.Core.Repositories;

/// <summary>
/// Generic concrete repository for entities of the Transactions module.
/// Specializes Repository&lt;TEntity&gt; by injecting TransactionsDbContext.
/// </summary>
public class TransactionsRepository<TEntity> : Repository<TEntity>
    where TEntity : class
{
    public TransactionsRepository(TransactionsDbContext context) : base(context)
    {
    }
}
