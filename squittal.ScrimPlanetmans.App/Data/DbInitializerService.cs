using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace squittal.ScrimPlanetmans.App.Data;

public class DbInitializerService
{
    private readonly ILogger<DbInitializerService> _logger;
    private readonly PlanetmansDbContext _dbContext;

    public DbInitializerService(ILogger<DbInitializerService> logger, PlanetmansDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public bool Initialize()
    {
        try
        {
            _dbContext.Database.Migrate();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate the database");
            return false;
        }
    }
}
