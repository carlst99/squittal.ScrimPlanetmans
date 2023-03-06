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

public class FactionService : IFactionService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly CensusFaction _censusFaction;
    private readonly ISqlScriptRunner _sqlScriptRunner;
    private readonly ILogger<FactionService> _logger;

    public string BackupSqlScriptFileName => Path.Combine("CensusBackups", "dbo.Faction.Table.sql");


    public FactionService(IDbContextHelper dbContextHelper, CensusFaction censusFaction, ISqlScriptRunner sqlScriptRunner, ILogger<FactionService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _censusFaction = censusFaction;
        _sqlScriptRunner = sqlScriptRunner;
        _logger = logger;
    }

    public async Task<IEnumerable<Faction>> GetAllFactionsAsync()
    {
        using (var factory = _dbContextHelper.GetFactory())
        {
            var dbContext = factory.GetDbContext();

            return await dbContext.Factions.ToListAsync();
        }

    }

    public async Task<Faction?> GetFactionAsync(int factionId)
    {
        var factions = await GetAllFactionsAsync();
        return factions.FirstOrDefault(f => f.Id == factionId);
    }

    public string GetFactionAbbrevFromId(int factionId)
    {
        return factionId switch
        {
            1 => "VS",
            2 => "NC",
            3 => "TR",
            4 => "NSO",
            _ => string.Empty
        };
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

            var anyFactions = await dbContext.Factions.AnyAsync(cancellationToken: ct);
            if (anyFactions)
            {
                return;
            }
        }

        var success = await RefreshStoreFromCensus();

        if (!success && canUseBackupScript)
        {
            RefreshStoreFromBackup(ct);
        }
    }

    public async Task<bool> RefreshStoreFromCensus()
    {
        IEnumerable<CensusFactionModel> factions = new List<CensusFactionModel>();

        try
        {
            factions = await _censusFaction.GetAllFactions();
        }
        catch
        {
            _logger.LogError("Census API query failed: get all Factions. Refreshing store from backup...");
            return false;
        }

        if (factions != null && factions.Any())
        {
            var censusEntities = factions.Select(ConvertToDbModel);

            using (var factory = _dbContextHelper.GetFactory())
            {
                var dbContext = factory.GetDbContext();

                var storedEntities = await dbContext.Factions.ToListAsync();

                var createdEntities = new List<Faction>();

                foreach (var censusEntity in censusEntities)
                {
                    var storeEntity = storedEntities.FirstOrDefault(storedEntity => storedEntity.Id == censusEntity.Id);
                    if (storeEntity == null)
                    {
                        createdEntities.Add(censusEntity);
                    }
                    else
                    {
                        storeEntity = censusEntity;
                        dbContext.Factions.Update(storeEntity);
                    }
                }

                if (createdEntities.Any())
                {
                    await dbContext.Factions.AddRangeAsync(createdEntities);
                }

                await dbContext.SaveChangesAsync();

                _logger.LogInformation("Refreshed Factions store");
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    public static Faction ConvertToDbModel(CensusFactionModel censusModel)
    {
        return new Faction
        {
            Id = censusModel.FactionId,
            Name = censusModel.Name.English,
            ImageId = censusModel.ImageId,
            CodeTag = censusModel.CodeTag,
            UserSelectable = censusModel.UserSelectable
        };
    }

    public async Task<int> GetCensusCountAsync()
    {
        return await _censusFaction.GetFactionsCount();
    }

    public async Task<int> GetStoreCountAsync()
    {
        using var factory = _dbContextHelper.GetFactory();
        var dbContext = factory.GetDbContext();

        return await dbContext.Factions.CountAsync();
    }

    public void RefreshStoreFromBackup(CancellationToken ct = default)
    {
        _sqlScriptRunner.RunSqlScript(BackupSqlScriptFileName);
    }
}
