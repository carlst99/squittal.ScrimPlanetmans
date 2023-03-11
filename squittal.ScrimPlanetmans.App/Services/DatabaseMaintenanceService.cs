using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Logging;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services;

public class DatabaseMaintenanceService
{
    private readonly ISqlScriptRunner _adhocScriptRunner;

    private bool _isInitialLoadComplete;

    public List<CensusStoreDataComparisonRow> Comparisons { get; } = new();

    public DatabaseMaintenanceService
    (
        IFacilityService facilityService,
        IZoneService zoneService,
        IWorldService worldService,
        IVehicleService vehicleService,
        ISqlScriptRunner adhocScriptRunner
    )
    {
        _adhocScriptRunner = adhocScriptRunner;

        CensusStoreDataComparisonRow mapRegions = new("Map Regions", facilityService);
        CensusStoreDataComparisonRow zones = new("Zones", zoneService);
        CensusStoreDataComparisonRow worlds = new("Worlds", worldService);
        CensusStoreDataComparisonRow vehicles = new("Vehicles", vehicleService);

        Comparisons.Add(mapRegions);
        Comparisons.Add(zones);
        Comparisons.Add(worlds);
        Comparisons.Add(vehicles);
    }

    public async Task InitializeCounts()
    {
        if (_isInitialLoadComplete)
        {
            return;
        }
        else
        {
            await SetAllCounts();
            _isInitialLoadComplete = true;
        }
    }

    public async Task SetAllCounts()
    {
        var TaskList = new List<Task>();

        foreach (var comparisonRow in Comparisons)
        {
            TaskList.Add(comparisonRow.SetCounts());
        }

        await Task.WhenAll(TaskList);
    }

    public async Task RefreshAllFromCensusAsync(CancellationToken ct)
    {
        var TaskList = new List<Task>();

        foreach (var comparisonRow in Comparisons)
        {
            TaskList.Add(comparisonRow.RefreshStoreFromCensusAsync(ct));
        }

        await Task.WhenAll(TaskList);
    }

    public async Task RefreshAllFromBackupAsync(CancellationToken ct)
    {
        var TaskList = new List<Task>();

        foreach (var comparisonRow in Comparisons)
        {
            TaskList.Add(comparisonRow.RefreshStoreFromBackupAsync(ct));
        }

        await Task.WhenAll(TaskList);
    }

    public IEnumerable<string> GetAdHocSqlFileNames()
    {
        return SqlScriptFileHandler.GetAdHocSqlFileNames();
    }

    public bool TryRunAdHocSqlScript(string fileName, out string info)
    {
        return _adhocScriptRunner.TryRunAdHocSqlScript(fileName, out info);
    }
}
