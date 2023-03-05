using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

public class ZoneService : IZoneService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusZone _censusZone;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<ZoneService> _logger;

    private readonly ConcurrentDictionary<int, Zone> _zoneMaps = new();
    private readonly SemaphoreSlim _mapSetUpSemaphore = new(1);

    public string BackupSqlScriptFileName => "CensusBackups\\dbo.Zone.Table.sql";

    public ZoneService(IDbContextHelper dbContextHelper, CensusZone censusZone, ISqlScriptRunner sqlScriptRunner, ILogger<ZoneService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusZone = censusZone;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task<IEnumerable<Zone>> GetAllZones()
    {
        if (_zoneMaps.IsEmpty)
            await SetupZonesMapAsync();

        return _zoneMaps.Values.ToList();
    }

    public async Task<IEnumerable<Zone>> GetAllZonesAsync()
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.Zones.ToListAsync();
    }

    public async Task<Zone?> GetZoneAsync(int zoneId)
    {
        if (_zoneMaps.IsEmpty)
            await SetupZonesMapAsync();

        _zoneMaps.TryGetValue(zoneId, out Zone? zone);

        return zone;
    }

    public async Task SetupZonesMapAsync()
    {
        await _mapSetUpSemaphore.WaitAsync();

        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            List<Zone> storeZones = await dbContext.Zones.ToListAsync();

            foreach (int zoneId in _zoneMaps.Keys)
            {
                if (storeZones.All(z => z.Id != zoneId))
                    _zoneMaps.TryRemove(zoneId, out _);
            }

            foreach (Zone zone in storeZones)
            {
                if (_zoneMaps.ContainsKey(zone.Id))
                    _zoneMaps[zone.Id] = zone;
                else
                    _zoneMaps.TryAdd(zone.Id, zone);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Zones Map");
        }
        finally
        {
            _mapSetUpSemaphore.Release();
        }
    }

    public async Task RefreshStore(bool onlyQueryCensusIfEmpty = false, bool canUseBackupScript = false)
    {
        if (onlyQueryCensusIfEmpty)
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            bool anyZones = await dbContext.Zones.AnyAsync();
            if (anyZones)
            {
                await SetupZonesMapAsync();

                return;
            }
        }

        bool success = await RefreshStoreFromCensus();

        if (!success && canUseBackupScript)
            RefreshStoreFromBackup();

        await SetupZonesMapAsync();
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        List<Zone> createdEntities = new();
        List<CensusZoneModel> zones;

        try
        {
            zones = (await _censusZone.GetAllZones()).ToList();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all Zones. Refreshing store from backup...");
            return false;
        }

        if (zones.Count is 0)
            return false;

        IEnumerable<Zone> censusEntities = zones.Select(ConvertToDbModel);

        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        List<Zone> storedEntities = await dbContext.Zones.ToListAsync();

        foreach (Zone censusEntity in censusEntities)
        {
            Zone? storeEntity = storedEntities.FirstOrDefault(storedEntity => storedEntity.Id == censusEntity.Id);
            if (storeEntity == null)
            {
                createdEntities.Add(censusEntity);
            }
            else
            {
                storeEntity = censusEntity;
                dbContext.Zones.Update(storeEntity);
            }
        }

        if (createdEntities.Any())
            await dbContext.Zones.AddRangeAsync(createdEntities);

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Refreshed Zones store");

        return true;
    }

    public static Zone ConvertToDbModel(CensusZoneModel censusModel)
    {
        string defaultZoneName = "Zone " + censusModel.ZoneId;

        return new Zone
        {
            Id = censusModel.ZoneId,
            Name = censusModel.Name?.English ?? defaultZoneName,
            Description = censusModel.Description?.English ?? defaultZoneName,
            Code = censusModel.Code,
            HexSize = censusModel.HexSize
        };
    }

    public async Task<int> GetCensusCountAsync()
        => await _censusZone.GetZonesCount();

    public async Task<int> GetStoreCountAsync()
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.Zones.CountAsync();
    }

    public void RefreshStoreFromBackup()
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }
}
