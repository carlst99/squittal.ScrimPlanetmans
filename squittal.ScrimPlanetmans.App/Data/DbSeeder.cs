using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class DbSeeder : IDbSeeder
{
    private readonly IScrimRulesetManager _rulesetManager;
    private readonly ISqlScriptService _sqlScriptService;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder
    (
        IScrimRulesetManager rulesetManager,
        ISqlScriptService sqlScriptService,
        ILogger<DbSeeder> logger
    )
    {
        _rulesetManager = rulesetManager;
        _sqlScriptService = sqlScriptService;
        _logger = logger;
    }

    public async Task SeedDatabase(CancellationToken cancellationToken)
    {
        try
        {
            await _rulesetManager.SeedScrimActionModelsAsync(cancellationToken);

            _sqlScriptService.RunSqlDirectoryScripts("Views");
            _logger.LogInformation("Compiled all SQL Views");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed database");
        }
    }
}
