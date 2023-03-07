﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusServices.Models;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class FacilityService : IFacilityService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusFacility _censusFacility;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<FacilityService> _logger;

    private readonly ConcurrentDictionary<int, MapRegion> _scrimmableFacilityMapRegionsMap = new();
    private readonly SemaphoreSlim _mapSetUpSemaphore = new(1);

    public string BackupSqlScriptFileName => Path.Combine("CensusBackups", "dbo.MapRegion.Table.sql");

    public FacilityService(IDbContextHelper dbContextHelper, CensusFacility censusFacility, ISqlScriptRunner sqlScriptRunner, ILogger<FacilityService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusFacility = censusFacility;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public Task<MapRegion> GetMapRegionAsync(int mapRegionId)
    {
        throw new System.NotImplementedException();
    }

    public Task<MapRegion> GetMapRegionFromFacilityIdAsync(int facilityId)
    {
        throw new System.NotImplementedException();
    }

    public Task<MapRegion> GetMapRegionFromFacilityNameAsync(string facilityName)
    {
        throw new System.NotImplementedException();
    }

    public Task<MapRegion> GetMapRegionsByFacilityTypeAsync(int facilityTypeId)
    {
        throw new System.NotImplementedException();
    }

    public async Task<IEnumerable<MapRegion>> GetScrimmableMapRegionsAsync()
    {
        if (_scrimmableFacilityMapRegionsMap.Count == 0 || !_scrimmableFacilityMapRegionsMap.Any())
        {
            await SetUpScrimmableMapRegionsAsync();
        }

        return GetScrimmableMapRegions();
    }

    private IEnumerable<MapRegion> GetScrimmableMapRegions()
    {
        return _scrimmableFacilityMapRegionsMap.Values.ToList();
    }

    public async Task<MapRegion> GetScrimmableMapRegionFromFacilityIdAsync(int facilityId)
    {
        if (_scrimmableFacilityMapRegionsMap.Count == 0 || !_scrimmableFacilityMapRegionsMap.Any())
        {
            await SetUpScrimmableMapRegionsAsync();
        }

        return GetScrimmableMapRegionFromFacilityId(facilityId);
    }

    private MapRegion GetScrimmableMapRegionFromFacilityId(int facilityId)
    {
        _scrimmableFacilityMapRegionsMap.TryGetValue(facilityId, out var mapRegion);

        return mapRegion;
    }

    public async Task SetUpScrimmableMapRegionsAsync()
    {
        await _mapSetUpSemaphore.WaitAsync();

        try
        {
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var storeRegions = await GetAllStoredScrimmableZoneMapRegionsAsync();

            foreach (var facilityId in _scrimmableFacilityMapRegionsMap.Keys)
            {
                if (!storeRegions.Any(r => r.FacilityId == facilityId))
                {
                    _scrimmableFacilityMapRegionsMap.TryRemove(facilityId, out var removedItem);
                }
            }

            foreach (var region in storeRegions)
            {
                if (_scrimmableFacilityMapRegionsMap.ContainsKey(region.FacilityId))
                {
                    _scrimmableFacilityMapRegionsMap[region.FacilityId] = region;
                }
                else
                {
                    _scrimmableFacilityMapRegionsMap.TryAdd(region.FacilityId, region);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Scrimmable Map Regions Map");
        }
        finally
        {
            _mapSetUpSemaphore.Release();
        }
    }

    private async Task<IEnumerable<MapRegion>> GetAllStoredScrimmableZoneMapRegionsAsync()
    {
        var realZones = new List<int> { 2, 4, 6, 8 };
        var scrimFacilityTypes = new List<int> { 5, 6 }; // Small Outpost, Large Outpost

        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.MapRegions.Where(region => realZones.Contains(region.ZoneId) && scrimFacilityTypes.Contains(region.FacilityTypeId) && region.IsCurrent)
            .ToListAsync();
    }

    private async Task<IEnumerable<MapRegion>> GetAllStoreMapRegionsAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.MapRegions.ToListAsync();
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
            if (await GetStoreCountAsync() > 0)
            {
                await SetUpScrimmableMapRegionsAsync();

                return;
            }
        }

        // Always use backup script if available to get old bases that aren't in the Census API
        if (canUseBackupScript)
        {
            RefreshStoreFromBackup(ct);
        }

        await RefreshStoreFromCensus();

        await SetUpScrimmableMapRegionsAsync();
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        IEnumerable<CensusMapRegionModel> mapRegions = new List<CensusMapRegionModel>();

        try
        {
            mapRegions = await _censusFacility.GetAllMapRegions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Census API query failed: get all Map Regions. Refreshing store from backup...");
            return false;
        }

        if (mapRegions != null && mapRegions.Any())
        {
            await UpsertRangeAsync(mapRegions.Select(ConvertToDbModel));

            _logger.LogInformation($"Refreshed Map Regions store: {mapRegions.Count()} entries");

            return true;
        }
        else
        {
            return false;
        }

    }

    private async Task UpsertRangeAsync(IEnumerable<MapRegion> censusEntities)
    {
        var createdEntities = new List<MapRegion>();

        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        var storeEntities = await GetAllStoreMapRegionsAsync();

        var allEntities = new List<MapRegion>(censusEntities.Select(e => new MapRegion() { Id = e.Id, FacilityId = e.FacilityId }));

        allEntities.AddRange(storeEntities
            .Where(s => !allEntities.Any(c => c.Id == s.Id && c.FacilityId == s.FacilityId))
            .Select(e => new MapRegion() { Id = e.Id, FacilityId = e.FacilityId }));

        foreach (var entity in allEntities)
        {
            var censusEntity = censusEntities.FirstOrDefault(e => e.Id == entity.Id && e.FacilityId == entity.FacilityId);
            var censusMapRegion = censusEntities.FirstOrDefault(e => e.Id == entity.Id);

            var storeEntity = storeEntities.FirstOrDefault(e => e.Id == entity.Id && e.FacilityId == entity.FacilityId);
            var storeMapRegion = storeEntities.FirstOrDefault(e => e.Id == entity.Id);

            if (censusEntity != null)
            {
                if (storeEntity == null)
                {
                    // Brand New MapRegion
                    if (storeMapRegion == null)
                    {
                        censusEntity.IsDeprecated = false;
                        censusEntity.IsCurrent = true;

                        createdEntities.Add(censusEntity);
                    }
                    // Existing MapRegion overwritten with new FacilityID
                    else if (censusEntity.FacilityId != 0)
                    {
                        censusEntity.IsDeprecated = false;
                        censusEntity.IsCurrent = true;

                        createdEntities.Add(censusEntity);

                        storeEntity = storeMapRegion;
                        storeEntity.IsDeprecated = true;
                        storeEntity.IsCurrent = false;

                        dbContext.MapRegions.Update(storeEntity);
                    }
                    // Existing MapRegion is Deleted with no replacement, but still shows up in the Census API
                    else
                    {
                        storeEntity = storeMapRegion;
                        storeEntity.IsDeprecated = true;
                        storeEntity.IsCurrent = true;

                        dbContext.MapRegions.Update(storeEntity);
                    }
                }
                // Existing MapRegion updated somehow
                else
                {
                    storeEntity = censusEntity;

                    storeEntity.IsDeprecated = false;
                    storeEntity.IsCurrent = true;

                    dbContext.MapRegions.Update(storeEntity);
                }
            }
            // Existing MapRegion is Deleted with no replacement, and doesn't show up in the Census API
            else
            {
                if (storeEntity != null)
                {
                    storeEntity.IsDeprecated = true;
                    storeEntity.IsCurrent = (censusMapRegion != null && censusMapRegion.Id == storeEntity.Id) ? false : true;

                    dbContext.MapRegions.Update(storeEntity);
                }
            }
        }

        if (createdEntities.Any())
        {
            dbContext.MapRegions.AddRange(createdEntities);
        }

        await dbContext.SaveChangesAsync();
    }

    private MapRegion ConvertToDbModel(CensusMapRegionModel censusModel)
    {
        return new MapRegion
        {
            Id = censusModel.MapRegionId,
            FacilityId = censusModel.FacilityId,
            FacilityName = censusModel.FacilityName,
            FacilityTypeId = censusModel.FacilityTypeId,
            FacilityType = censusModel.FacilityType,
            ZoneId = censusModel.ZoneId
        };
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusFacility.GetMapRegionsCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.MapRegions.CountAsync(e => e.IsCurrent);
    }

    public void RefreshStoreFromBackup(CancellationToken ct = default)
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }
}
