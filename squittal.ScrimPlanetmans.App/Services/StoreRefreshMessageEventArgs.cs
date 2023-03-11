using System;

namespace squittal.ScrimPlanetmans.App.Services;

public class StoreRefreshMessageEventArgs : EventArgs
{
    public static readonly StoreRefreshMessageEventArgs Default = new();

    private StoreRefreshMessageEventArgs()
    {
    }
}
