using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class ApplicationDataLoader : IApplicationDataLoader
{
    private readonly ILogger<ApplicationDataLoader> _logger;
    private readonly IScrimRulesetManager _rulesetManager;

    public ApplicationDataLoader
    (
        ILogger<ApplicationDataLoader> logger,
        IScrimRulesetManager rulesetManager
    )
    {
        _logger = logger;
        _rulesetManager = rulesetManager;
    }

    public async Task OnApplicationStartup(CancellationToken cancellationToken)
    {
        try
        {
            await _rulesetManager.SeedDefaultRulesetAsync(cancellationToken);
            await _rulesetManager.ActivateDefaultRulesetAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading application data");
        }
    }

    public async Task OnApplicationShutdown(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}
