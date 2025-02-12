﻿using System.Threading;
using System.Threading.Tasks;

namespace squittal.ScrimPlanetmans.App.Data.Interfaces;

public interface IApplicationDataLoader
{
    Task OnApplicationStartup(CancellationToken cancellationToken);
    Task OnApplicationShutdown(CancellationToken cancellationToken);
}
