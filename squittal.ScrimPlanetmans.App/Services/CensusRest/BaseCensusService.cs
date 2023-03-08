using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Logging;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

public abstract class BaseCensusService
{
    private readonly ILogger<BaseCensusService> _logger;
    
    protected IQueryService QueryService { get; }

    protected BaseCensusService
    (
        ILogger<BaseCensusService> logger,
        IQueryService queryService
    )
    {
        _logger = logger;
        QueryService = queryService;
    }

    protected async Task<T?> GetAsync<T>
    (
        IQueryBuilder query,
        CancellationToken ct = default,
        [CallerMemberName] string? caller = null
    ) where T : class
    {
        try
        {
            return await QueryService.GetAsync<T>(query, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to GET value for caller {CallerName}", caller);
            return null;
        }
    }

    protected async Task<IReadOnlyList<T>?> GetListAsync<T>
    (
        IQueryBuilder query,
        CancellationToken ct = default,
        [CallerMemberName] string? caller = null
    ) => await GetAsync<IReadOnlyList<T>>(query, ct, caller);

    protected async Task<ulong?> CountAsync
    (
        IQueryBuilder query,
        CancellationToken ct = default,
        [CallerMemberName] string? caller = null
    )
    {
        try
        {
            return await QueryService.CountAsync(query, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to COUNT value for caller {CallerName}", caller);
            return null;
        }
    }
}
