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
    private readonly IFacilityTypeService _facilityTypeService;
    private readonly IFacilityService _facilityService;
    private readonly IItemService _itemService;
    private readonly IItemCategoryService _itemCategoryService;
    private readonly IZoneService _zoneService;
    private readonly IWorldService _worldService;
    private readonly IVehicleService _vehicleService;

    private readonly ISqlScriptRunner _adhocScriptRunner;

    private readonly CensusStoreDataComparisonRow _mapRegions;
    private readonly CensusStoreDataComparisonRow _facilityTypes;
    private readonly CensusStoreDataComparisonRow _items;
    private readonly CensusStoreDataComparisonRow _itemCategories;
    private readonly CensusStoreDataComparisonRow _profiles;
    private readonly CensusStoreDataComparisonRow _loadouts;
    private readonly CensusStoreDataComparisonRow _zones;
    private readonly CensusStoreDataComparisonRow _worlds;
    private readonly CensusStoreDataComparisonRow _vehicles;

    public List<CensusStoreDataComparisonRow> Comparisons { get; private set; } = new List<CensusStoreDataComparisonRow>();

    private bool _isInitialLoadComplete = false;

    public DatabaseMaintenanceService(
        IFacilityTypeService facilityTypeService,
        IFacilityService facilityService,
        IItemService itemService,
        IItemCategoryService itemCategoryService,
        IZoneService zoneService,
        IWorldService worldService,
        IVehicleService vehicleService,
        ISqlScriptRunner adhocScriptRunner
    )
    {
        _facilityService = facilityService;
        _facilityTypeService = facilityTypeService;
        _itemService = itemService;
        _itemCategoryService = itemCategoryService;
        _zoneService = zoneService;
        _worldService = worldService;
        _vehicleService = vehicleService;
        _adhocScriptRunner = adhocScriptRunner;

        _mapRegions = new CensusStoreDataComparisonRow("Map Regions", _facilityService);
        _facilityTypes = new CensusStoreDataComparisonRow("Facility Types", _facilityTypeService);
        _items = new CensusStoreDataComparisonRow("Items", _itemService);
        _itemCategories = new CensusStoreDataComparisonRow("Item Categories", _itemCategoryService);
        _zones = new CensusStoreDataComparisonRow("Zones", _zoneService);
        _worlds = new CensusStoreDataComparisonRow("Worlds", _worldService);
        _vehicles = new CensusStoreDataComparisonRow("Vehicles", _vehicleService);

        Comparisons.Add(_mapRegions);
        Comparisons.Add(_facilityTypes);
        Comparisons.Add(_items);
        Comparisons.Add(_itemCategories);
        Comparisons.Add(_profiles);
        Comparisons.Add(_loadouts);
        Comparisons.Add(_zones);
        Comparisons.Add(_worlds);
        Comparisons.Add(_vehicles);
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
