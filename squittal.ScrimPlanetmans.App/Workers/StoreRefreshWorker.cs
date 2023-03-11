using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;

namespace squittal.ScrimPlanetmans.App.Workers;

public class StoreRefreshWorker : BackgroundService
{
    private readonly ILogger<StoreRefreshWorker> _logger;
    private readonly IRulesetDataService _rulesetDataService;

    public StoreRefreshWorker(ILogger<StoreRefreshWorker> logger, IRulesetDataService rulesetDataService)
    {
        _logger = logger;
        _rulesetDataService = rulesetDataService;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        PeriodicTimer timer = new(TimeSpan.FromHours(24));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _rulesetDataService.RefreshRulesetsAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // This is fine
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run an interation of the StoreRefreshWorker");
            }

            await timer.WaitForNextTickAsync(ct);
        }
    }
}
