using Microsoft.EntityFrameworkCore;

namespace TrainingRegistry.Infrastructure;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize < 1) pageSize = 20;
        // сначала считаем всего, потом берём только нужную страницу
        var total = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sort,
        bool descending,
        IReadOnlyDictionary<string, System.Linq.Expressions.Expression<Func<T, object?>>> selectors,
        string defaultKey)
    {
        // если ключ сортировки незнакомый - берём сортировку по умолчанию
        var key = sort != null && selectors.ContainsKey(sort) ? sort : defaultKey;
        var selector = selectors[key];
        return descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
    }
}