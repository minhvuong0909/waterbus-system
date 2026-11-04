using Microsoft.EntityFrameworkCore;
using WaterbusSystem.Application.Common.Models;

namespace WaterbusSystem.Infrastructure.Persistence;

/// <summary>
/// Thực thi truy vấn EF Core (CountAsync/ToListAsync) để dựng PaginatedList - tách khỏi Application
/// vì PaginatedList ở đó chỉ còn là data shape thuần, không phụ thuộc EF Core.
/// </summary>
public static class PaginatedListFactory
{
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> source, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var count = await source.CountAsync(cancellationToken);
        var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PaginatedList<T>(items, count, pageNumber, pageSize);
    }
}
