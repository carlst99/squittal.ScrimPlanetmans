﻿using System;

namespace squittal.ScrimPlanetmans.App.Services;

public class StoreRefreshMessageEventArgs : EventArgs
{
    public StoreRefreshSource RefreshSource { get; set; }

    public StoreRefreshMessageEventArgs(StoreRefreshSource refreshSource)
    {
        RefreshSource = refreshSource;
    }
}

public enum StoreRefreshSource
{
    CensusApi,
    BackupSqlScript
}
