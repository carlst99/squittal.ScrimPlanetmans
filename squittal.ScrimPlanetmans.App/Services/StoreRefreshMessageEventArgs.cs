using System;
using System.Threading;

namespace squittal.ScrimPlanetmans.App.Services;

public class StoreRefreshMessageEventArgs : EventArgs
{
    public StoreRefreshSource RefreshSource { get; set; }
    public CancellationToken CancellationToken { get; set; }

    public StoreRefreshMessageEventArgs(StoreRefreshSource refreshSource, CancellationToken cancellationToken)
    {
        RefreshSource = refreshSource;
        CancellationToken = cancellationToken;
    }
}

public enum StoreRefreshSource
{
    CensusApi,
    BackupSqlScript
}
