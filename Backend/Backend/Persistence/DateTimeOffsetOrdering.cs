using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

internal static class DateTimeOffsetOrdering
{
    public static async Task<List<TSource>> ToAscendingListAsync<TSource>(
        IQueryable<TSource> source,
        DbContext dbContext,
        Expression<Func<TSource, DateTimeOffset>> keySelector,
        CancellationToken cancellationToken)
    {
        if (!DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return await source.OrderBy(keySelector).ToListAsync(cancellationToken);
        }

        var key = keySelector.Compile();
        var items = await source.ToListAsync(cancellationToken);
        return items.OrderBy(key).ToList();
    }

    public static async Task<List<TSource>> ToDescendingListAsync<TSource>(
        IQueryable<TSource> source,
        DbContext dbContext,
        Expression<Func<TSource, DateTimeOffset>> keySelector,
        CancellationToken cancellationToken)
    {
        if (!DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return await source.OrderByDescending(keySelector).ToListAsync(cancellationToken);
        }

        var key = keySelector.Compile();
        var items = await source.ToListAsync(cancellationToken);
        return items.OrderByDescending(key).ToList();
    }
}
