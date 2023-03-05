namespace squittal.ScrimPlanetmans.App.Services.Interfaces;

public interface ILocallyBackedStore
{
    string BackupSqlScriptFileName { get; }

    void RefreshStoreFromBackup();
}
