﻿using System.Collections.Generic;
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

public class LoadoutService : ILoadoutService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusLoadout _censusLoadout;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<LoadoutService> _logger;

    public string BackupSqlScriptFileName => Path.Combine("CensusBackups", "dbo.Loadout.Table.sql");


    public LoadoutService(IDbContextHelper dbContextHelper, CensusLoadout censusLoadout, ISqlScriptRunner sqlScriptRunner, ILogger<LoadoutService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusLoadout = censusLoadout;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusLoadout.GetLoadoutsCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.Loadouts.CountAsync();
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
            using var factory = _dbContextHelper.GetFactory();
            var dbContext = factory.GetDbContext();

            var anyLoadouts = await dbContext.Loadouts.AnyAsync(cancellationToken: ct);
            if (anyLoadouts)
            {
                return;
            }
        }

        bool success = await RefreshStoreFromCensus(ct);

        if (!success && canUseBackupScript)
            RefreshStoreFromBackup(ct);
    }

    public async Task<bool> RefreshStoreFromCensus(CancellationToken ct = default)
    {
        CensusLoadoutModel[] censusLoadouts;

        try
        {
            censusLoadouts = (await _censusLoadout.GetAllLoadoutsAsync()).ToArray();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all Loadouts. Refreshing store from backup...");
            return false;
        }

        if (censusLoadouts.Any())
        {
            List<CensusLoadoutModel> allLoadouts = new List<CensusLoadoutModel>();

            allLoadouts.AddRange(censusLoadouts.ToList());
            allLoadouts.AddRange(GetFakeNsCensusLoadoutModels());

            await UpsertRangeAsync(allLoadouts.AsEnumerable().Select(ConvertToDbModel));

            _logger.LogInformation("Refreshed Loadouts store");

            return true;
        }
        else
        {
            return false;
        }
    }

    private async Task UpsertRangeAsync(IEnumerable<Loadout> censusEntities)
    {
        var createdEntities = new List<Loadout>();

        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        var storedEntities = await dbContext.Loadouts.ToListAsync();

        foreach (var censusEntity in censusEntities)
        {
            var storeEntity = storedEntities.FirstOrDefault(e => e.Id == censusEntity.Id);
            if (storeEntity == null)
            {
                createdEntities.Add(censusEntity);
            }
            else
            {
                storeEntity = censusEntity;
                dbContext.Loadouts.Update(storeEntity);
            }
        }

        if (createdEntities.Any())
        {
            await dbContext.Loadouts.AddRangeAsync(createdEntities);
        }

        await dbContext.SaveChangesAsync();
    }

    private Loadout ConvertToDbModel(CensusLoadoutModel censusModel)
    {
        return new Loadout
        {
            Id = censusModel.LoadoutId,
            ProfileId = censusModel.ProfileId,
            FactionId = censusModel.FactionId,
            CodeName = censusModel.CodeName,
        };
    }

    private static IEnumerable<CensusLoadoutModel> GetFakeNsCensusLoadoutModels()
    {
        var nsLoadouts = new List<CensusLoadoutModel>
        {
            GetNewCensusLoadoutModel(28, 190, 4, "NS Infiltrator"),
            GetNewCensusLoadoutModel(29, 191, 4, "NS Light Assault"),
            GetNewCensusLoadoutModel(30, 192, 4, "NS Combat Medic"),
            GetNewCensusLoadoutModel(31, 193, 4, "NS Engineer"),
            GetNewCensusLoadoutModel(32, 194, 4, "NS Heavy Assault"),
            GetNewCensusLoadoutModel(45, 252, 4, "NS Defector")
        };

        return nsLoadouts;
    }

    private static CensusLoadoutModel GetNewCensusLoadoutModel
    (
        int loadoutId,
        int profileId,
        int factionId,
        string codeName
    )
    {
        return new CensusLoadoutModel()
        {
            LoadoutId = loadoutId,
            ProfileId = profileId,
            FactionId = factionId,
            CodeName = codeName
        };
    }

    public void RefreshStoreFromBackup(CancellationToken ct = default)
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }
}
