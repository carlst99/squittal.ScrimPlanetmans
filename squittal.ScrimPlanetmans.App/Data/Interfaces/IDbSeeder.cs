using System.Threading;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Data.Interfaces;

public interface IDbSeeder
{
    Task SeedDatabase(CancellationToken cancellationToken);
}
