using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services;
using squittal.ScrimPlanetmans.App.Data.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class DbSeeder : IDbSeeder
{
    private readonly ISqlScriptService _sqlScriptService;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder
    (
        ISqlScriptService sqlScriptService,
        ILogger<DbSeeder> logger
    )
    {
        _sqlScriptService = sqlScriptService;
        _logger = logger;
    }

    public Task SeedDatabase(CancellationToken cancellationToken)
    {
        try
        {
            _sqlScriptService.RunSqlDirectoryScripts("Views");
            _logger.LogInformation("Compiled all SQL Views");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed database");
        }

        return Task.CompletedTask;
    }
}
