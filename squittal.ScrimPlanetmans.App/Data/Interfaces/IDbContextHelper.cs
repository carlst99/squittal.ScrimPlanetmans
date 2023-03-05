using static squittal.ScrimPlanetmans.App.Data.DbContextHelper;

namespace squittal.ScrimPlanetmans.App.Data.Interfaces;

public interface IDbContextHelper
{
    DbContextFactory GetFactory();
}
