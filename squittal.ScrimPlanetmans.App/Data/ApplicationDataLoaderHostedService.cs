using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using squittal.ScrimPlanetmans.App.Data.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class ApplicationDataLoaderHostedService : IHostedService
{
    private readonly IApplicationDataLoader _appLoader;

    public ApplicationDataLoaderHostedService(IApplicationDataLoader appLoader)
    {
        _appLoader = appLoader;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return _appLoader.OnApplicationStartup(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _appLoader.OnApplicationShutdown(cancellationToken);
    }
}
