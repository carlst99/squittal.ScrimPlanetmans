using System;
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
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class WorldService : IWorldService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusWorld _censusWorld;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<WorldService> _logger;

    private readonly ConcurrentDictionary<int, World> _worldsMap = new();
    private readonly SemaphoreSlim _mapSetUpSemaphore = new(1);

    public string BackupSqlScriptFileName => Path.Combine("CensusBackups", "dbo.World.Table.sql");


    public WorldService(IDbContextHelper dbContextHelper, CensusWorld censusWorld, ISqlScriptRunner sqlScriptRunner, ILogger<WorldService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusWorld = censusWorld;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }


    public async Task<IEnumerable<World>> GetAllWorldsAsync()
    {
        if (_worldsMap.Count == 0 || !_worldsMap.Any())
        {
            await SetUpWorldsMap();
        }

        return GetAllWorlds();
    }

    private IEnumerable<World> GetAllWorlds()
    {
        return _worldsMap.Values.ToList();
    }

    public async Task<World?> GetWorldAsync(int worldId)
    {
        if (_worldsMap.Count == 0 || !_worldsMap.Any())
        {
            await SetUpWorldsMap();
        }

        return GetWorld(worldId);
    }

    private World? GetWorld(int worldId)
    {
        _worldsMap.TryGetValue(worldId, out World? world);

        return world;
    }

    public async Task SetUpWorldsMap()
    {
        await _mapSetUpSemaphore.WaitAsync();

        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            List<World> storeWorlds = await dbContext.Worlds.Where(z => z.Id != 25).ToListAsync(); // RIP Briggs

            foreach (int worldId in _worldsMap.Keys)
            {
                if (storeWorlds.All(r => r.Id != worldId))
                {
                    _worldsMap.TryRemove(worldId, out _);
                }
            }

            foreach (World world in storeWorlds)
            {
                if (_worldsMap.ContainsKey(world.Id))
                {
                    _worldsMap[world.Id] = world;
                }
                else
                {
                    _worldsMap.TryAdd(world.Id, world);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up Worlds Map");
        }
        finally
        {
            _mapSetUpSemaphore.Release();
        }
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

            bool anyWorlds = await dbContext.Worlds.AnyAsync(cancellationToken: ct);
            if (anyWorlds)
            {
                await SetUpWorldsMap();

                return;
            }
        }

        bool success = await RefreshStoreFromCensus();
        if (!success && canUseBackupScript)
            RefreshStoreFromBackup(ct);

        await SetUpWorldsMap();
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        List<World> createdEntities = new();
        CensusWorldModel[] worlds;

        try
        {
            worlds = (await _censusWorld.GetAllWorlds()).ToArray();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all Worlds. Refreshing store from backup...");
            return false;
        }

        if (!worlds.Any())
            return false;


        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        IEnumerable<World> censusEntities = worlds.Select(ConvertToDbModel);
        List<World> storedEntities = await dbContext.Worlds.ToListAsync();

        foreach (World censusEntity in censusEntities)
        {
            World? storeEntity = storedEntities.FirstOrDefault(storedEntity => storedEntity.Id == censusEntity.Id);
            if (storeEntity == null)
            {
                createdEntities.Add(censusEntity);
            }
            else
            {
                storeEntity = censusEntity;
                dbContext.Worlds.Update(storeEntity);
            }
        }

        if (createdEntities.Any())
        {
            await dbContext.Worlds.AddRangeAsync(createdEntities);
        }

        await dbContext.SaveChangesAsync();
        _logger.LogInformation("Refreshed Worlds store");

        return true;
    }

    public static World ConvertToDbModel(CensusWorldModel censusModel)
    {
        return new World
        {
            Id = censusModel.WorldId,
            Name = censusModel.Name.English
        };
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusWorld.GetWorldsCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.Worlds.CountAsync();
    }

    public void RefreshStoreFromBackup(CancellationToken ct = default)
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }

    public static bool IsJaegerWorldId(int worldId)
    {
        return worldId == 19;
    }
}
