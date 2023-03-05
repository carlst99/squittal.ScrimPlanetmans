using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using squittal.ScrimPlanetmans.App.Data.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class DbContextHelper : IDbContextHelper
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DbContextHelper(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public DbContextFactory GetFactory()
    {
        return new DbContextFactory(_scopeFactory);
    }

    public class DbContextFactory : IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly PlanetmansDbContext _dbContext;

        public DbContextFactory(IServiceScopeFactory scopeFactory)
        {
            _scope = scopeFactory.CreateScope();
            _dbContext = _scope.ServiceProvider.GetRequiredService<PlanetmansDbContext>();

            _dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        public PlanetmansDbContext GetDbContext()
        {
            return _dbContext;
        }

        public void Dispose()
        {
            _dbContext.Dispose();
            _scope.Dispose();
        }
    }
}
