using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services;
using squittal.ScrimPlanetmans.App.Data;

namespace squittal.ScrimPlanetmans.App.Services;

public class DbInitializerService
{
    private readonly ILogger<DbInitializerService> _logger;
    private readonly PlanetmansDbContext _dbContext;
    private readonly ISqlScriptService _sqlScriptService;

    public DbInitializerService
    (
        ILogger<DbInitializerService> logger,
        PlanetmansDbContext dbContext,
        ISqlScriptService sqlScriptService
    )
    {
        _logger = logger;
        _dbContext = dbContext;
        _sqlScriptService = sqlScriptService;
    }

    public bool Initialize()
    {
        try
        {
            _dbContext.Database.Migrate();
            _sqlScriptService.RunSqlDirectoryScripts("Views");

            _logger.LogInformation("Migrated DB and compiled all SQL Views");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate the database");
            return false;
        }
    }
}
