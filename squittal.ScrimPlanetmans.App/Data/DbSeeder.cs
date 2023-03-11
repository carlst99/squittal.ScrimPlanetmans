using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Data;

public class DbSeeder : IDbSeeder
{
    private readonly IWorldService _worldService;
    private readonly IZoneService _zoneService;
    private readonly IScrimRulesetManager _rulesetManager;
    private readonly IFacilityService _facilityService;
    private readonly IVehicleService _vehicleService;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder
    (
        IWorldService worldService,
        IZoneService zoneService,
        IScrimRulesetManager rulesetManager,
        IFacilityService facilityService,
        IVehicleService vehicleService,
        ISqlScriptRunner sqlScriptRunner,
        ILogger<DbSeeder> logger
    )
    {
        _worldService = worldService;
        _zoneService = zoneService;
        _rulesetManager = rulesetManager;
        _facilityService = facilityService;
        _vehicleService = vehicleService;
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

            Task zoneTask = _zoneService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(zoneTask);

            Task scrimActionTask = _rulesetManager.SeedScrimActionModelsAsync(cancellationToken);
            TaskList.Add(scrimActionTask);

            Task facilitiesTask = _facilityService.RefreshStoreAsync(true, true, cancellationToken);
            TaskList.Add(facilitiesTask);

            Task vehicleTask = _vehicleService.RefreshStoreAsync(true, false, cancellationToken);
            TaskList.Add(vehicleTask);

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
