using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class DbSeeder : IDbSeeder
{
    private readonly IWorldService _worldService;
    private readonly IItemService _itemService;
    private readonly IItemCategoryService _itemCategoryService;
    private readonly IZoneService _zoneService;
    private readonly IProfileService _profileService;
    private readonly ILoadoutService _loadoutService;
    private readonly IScrimRulesetManager _rulesetManager;
    private readonly IFacilityService _facilityService;
    private readonly IFacilityTypeService _facilityTypeService;
    private readonly IVehicleService _vehicleService;
    private readonly IVehicleTypeService _vehicleTypeService;
    private readonly IDeathEventTypeService _deathTypeService;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder
    (
        IWorldService worldService,
        IItemService itemService,
        IItemCategoryService itemCategoryService,
        IZoneService zoneService,
        IProfileService profileService,
        ILoadoutService loadoutService,
        IScrimRulesetManager rulesetManager,
        IFacilityService facilityService,
        IFacilityTypeService facilityTypeService,
        IVehicleService vehicleService,
        IVehicleTypeService vehicleTypeService,
        IDeathEventTypeService deathTypeService,
        ISqlScriptRunner sqlScriptRunner,
        ILogger<DbSeeder> logger
    )
    {
        _worldService = worldService;
        _itemService = itemService;
        _itemCategoryService = itemCategoryService;
        _zoneService = zoneService;
        _profileService = profileService;
        _loadoutService = loadoutService;
        _rulesetManager = rulesetManager;
        _facilityService = facilityService;
        _facilityTypeService = facilityTypeService;
        _vehicleService = vehicleService;
        _vehicleTypeService = vehicleTypeService;
        _deathTypeService = deathTypeService;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task SeedDatabase(CancellationToken cancellationToken)
    {
        try
        {
            List<Task> TaskList = new();

            Task worldsTask = _worldService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(worldsTask);

            Task itemsTask = _itemService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(itemsTask);

            Task itemCategoriesTask = _itemCategoryService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(itemCategoriesTask);

            Task zoneTask = _zoneService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(zoneTask);

            Task profileTask = _profileService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(profileTask);

            Task loadoutsTask = _loadoutService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(loadoutsTask);

            Task scrimActionTask = _rulesetManager.SeedScrimActionModels();
            TaskList.Add(scrimActionTask);

            Task facilitiesTask = _facilityService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(facilitiesTask);

            Task facilityTypesTask = _facilityTypeService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(facilityTypesTask);

            Task vehicleTask = _vehicleService.RefreshStoreAsync(true, false, cancellationToken);
            TaskList.Add(vehicleTask);

            Task vehicleTypeTask = _vehicleTypeService.SeedVehicleClasses();
            TaskList.Add(vehicleTypeTask);

            Task deathTypeTask = _deathTypeService.SeedDeathTypes();
            TaskList.Add(deathTypeTask);

            await Task.WhenAll(TaskList);

            _sqlScriptRunner.RunSqlDirectoryScripts("Views");

            _logger.LogInformation($"Compiled all SQL Views");

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed database");
        }
    }
}
