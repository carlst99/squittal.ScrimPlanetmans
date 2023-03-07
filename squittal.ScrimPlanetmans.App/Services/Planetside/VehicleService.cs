using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusServices.Models;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class VehicleService : IVehicleService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusVehicle _censusVehicle;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<VehicleService> _logger;

    public string BackupSqlScriptFileName => Path.Combine("CensusBackups", "dbo.Vehicle.Table.sql");

    public VehicleService(IDbContextHelper dbContextHelper, CensusVehicle censusVehicle, ISqlScriptRunner sqlScriptRunner, ILogger<VehicleService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusVehicle = censusVehicle;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task<Vehicle?> GetVehicleInfoAsync(int vehicleId)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);
    }

    public Vehicle GetScrimVehicleInfo(int vehicleId)
    {
        throw new NotImplementedException();
    }

    public Task SetUpScrimmableVehicleInfosList()
    {
        throw new NotImplementedException();
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusVehicle.GetVehiclesCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.Vehicles.CountAsync();
    }

    public async Task RefreshStoreAsync
    (
        bool onlyQueryCensusIfEmpty = false,
        bool canUseBackupScript = false,
        CancellationToken ct = default
    )
    {
        if (onlyQueryCensusIfEmpty)
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            bool anyVehicles = await dbContext.Vehicles.AnyAsync(cancellationToken: ct);
            if (anyVehicles)
                return;
        }

        bool success = await RefreshStoreFromCensus();
        if (!success && canUseBackupScript)
            RefreshStoreFromBackup(ct);
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        CensusVehicleModel[] vehicles;

        try
        {
            vehicles = (await _censusVehicle.GetAllVehicles()).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Census API query failed: get all Vehicles. Refreshing store from backup...");
            return false;
        }

        if (!vehicles.Any())
            return false;

        await UpsertRangeAsync(vehicles.Select(ConvertToDbModel));
        _logger.LogInformation("Refreshed Vehicles store: {Count} entries", vehicles.Length);

        return true;
    }

    private async Task UpsertRangeAsync(IEnumerable<Vehicle> censusEntities)
    {
        List<Vehicle> createdEntities = new();

        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        List<Vehicle> storedEntities = await dbContext.Vehicles.ToListAsync();

        foreach (Vehicle censusEntity in censusEntities)
        {
            Vehicle? storeEntity = storedEntities.FirstOrDefault(e => e.Id == censusEntity.Id);
            if (storeEntity == null)
            {
                createdEntities.Add(censusEntity);
            }
            else
            {
                storeEntity = censusEntity;
                dbContext.Vehicles.Update(storeEntity);
            }
        }

        if (createdEntities.Any())
        {
            await dbContext.Vehicles.AddRangeAsync(createdEntities);
        }

        await dbContext.SaveChangesAsync();
    }

    public void RefreshStoreFromBackup(CancellationToken ct = default)
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }

    private static Vehicle ConvertToDbModel(CensusVehicleModel censusModel)
    {
        return new Vehicle
        {
            Id = censusModel.VehicleId,
            Name = censusModel.Name?.English,
            Description = censusModel.Description?.English,
            TypeId = censusModel.TypeId,
            TypeName = censusModel.TypeName,
            Cost = censusModel.Cost,
            CostResourceId = censusModel.CostResourceId,
            ImageId = censusModel.ImageId
        };
    }
}
