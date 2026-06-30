using System.Linq.Expressions;

namespace FinControl.SharedKernel.Domain;

/// <summary>
/// Base abstraction for implementing the Specification Pattern (agnóstico de ORM).
/// Encapsulates query logic for reusability and testability.
/// Implementações concretas devem ser criadas no Core.Features com suporte EF.
/// </summary>
/// <typeparam name="TEntity">The entity type to query</typeparam>
public abstract record SpecificationDomain<TEntity> where TEntity : class
{
    /// <summary>
    /// Gets the list of predicates to apply to the query.
    /// </summary>
    public List<Expression<Func<TEntity, bool>>> Criteria { get; } = [];

    /// <summary>
    /// Gets the list of includes (relationships) to eagerly load.
    /// </summary>
    public List<string> IncludeStrings { get; } = [];

    /// <summary>
    /// Gets the order by expressions.
    /// </summary>
    public List<(Expression<Func<TEntity, object>> KeySelector, bool IsDescending)> OrderExpressions { get; } = [];

    /// <summary>
    /// Gets the page index for pagination (1-based).
    /// </summary>
    public int PageIndex { get; protected set; } = 1;

    /// <summary>
    /// Gets the page size for pagination.
    /// </summary>
    public int PageSize { get; protected set; } = 10;

    /// <summary>
    /// Gets a value indicating whether pagination is enabled.
    /// </summary>
    public bool IsPagingEnabled { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether entity tracking is disabled (default: true).
    /// </summary>
    public bool IsTrackingDisabled { get; protected set; } = true;

    /// <summary>
    /// Adds a filter criterion to the specification.
    /// </summary>
    protected virtual void AddCriteria(Expression<Func<TEntity, bool>> criteria)
        => Criteria.Add(criteria);

    /// <summary>
    /// Adds an include (eager loading) using a string path (e.g., "Orders" or "Orders.Items").
    /// </summary>
    protected virtual void AddInclude(string includeString)
        => IncludeStrings.Add(includeString);

    /// <summary>
    /// Adds an include (eager loading) using a property expression.
    /// </summary>
    protected virtual void AddInclude(Expression<Func<TEntity, object?>> includeExpression)
        => IncludeStrings.Add(GetIncludePath(includeExpression));

    /// <summary>
    /// Adds a multilevel include (Include + ThenInclude) using expressions.
    /// </summary>
    protected virtual void AddInclude<TPreviousProperty>(
        Expression<Func<TEntity, IEnumerable<TPreviousProperty>?>> includeExpression,
        Expression<Func<TPreviousProperty, object?>> thenIncludeExpression)
        => IncludeStrings.Add($"{GetIncludePath(includeExpression)}.{GetIncludePath(thenIncludeExpression)}");

    /// <summary>
    /// Adds an order by expression in ascending order.
    /// </summary>
    protected virtual void AddOrderBy(Expression<Func<TEntity, object>> orderExpression)
        => OrderExpressions.Add((orderExpression, false));

    /// <summary>
    /// Adds an order by expression in descending order.
    /// </summary>
    protected virtual void AddOrderByDescending(Expression<Func<TEntity, object>> orderExpression)
        => OrderExpressions.Add((orderExpression, true));

    /// <summary>
    /// Enables pagination for the specification.
    /// </summary>
    protected virtual void EnablePaging(int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        IsPagingEnabled = true;
    }

    /// <summary>
    /// Enables entity change tracking.
    /// </summary>
    protected virtual void EnableTracking()
        => IsTrackingDisabled = false;

    /// <summary>
    /// Disables entity change tracking (default).
    /// </summary>
    protected virtual void DisableTracking()
        => IsTrackingDisabled = true;

    private static string GetIncludePath(LambdaExpression includeExpression)
    {
        Expression? expression = includeExpression.Body;

        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            expression = unary.Operand;

        var members = new Stack<string>();

        while (expression is MemberExpression memberExpression)
        {
            members.Push(memberExpression.Member.Name);
            expression = memberExpression.Expression;
        }

        if (members.Count == 0)
            throw new ArgumentException("Include expression must target a property path.", nameof(includeExpression));

        return string.Join('.', members);
    }
}


