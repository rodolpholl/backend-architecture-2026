namespace FinControl.SharedKernel.Domain;

/// <summary>Factory sem tipo genérico para evitar CA1000.</summary>
public static class PagedResult
{
    public static PagedResult<T> Create<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
        => new(items, page, pageSize, totalCount);
}

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    internal PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}
