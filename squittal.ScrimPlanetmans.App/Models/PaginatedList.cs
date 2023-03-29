using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace squittal.ScrimPlanetmans.App.Models;

public class PaginatedList<T>
{
    public int PageIndex { get; }
    public int PageCount { get; }
    public List<T> Contents { get; }

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < PageCount;

    public PaginatedList(int pageSize = 10)
        : this(Array.Empty<T>(), 0, 1, pageSize)
    {
        Contents = new List<T>();
    }

    public PaginatedList(IEnumerable<T> contents, int totalCount, int pageIndex = 1, int pageSize = 10)
    {
        PageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

        if (pageIndex < 0)
            PageIndex = 1;
        else if (pageIndex > PageCount)
            PageIndex = PageCount;
        else
            PageIndex = pageIndex;

        Contents = contents.ToList();
    }

    public static async Task<PaginatedList<T>> CreateAsync
    (
        IQueryable<T> source,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        int count = await source.CountAsync(cancellationToken);

        List<T> items = await source.Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }
}
