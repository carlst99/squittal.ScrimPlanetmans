using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch;

public class ScrimMatchDataService : IScrimMatchDataService
{
    private readonly IDbContextFactory<PlanetmansDbContext> _dbContextFactory;
    private readonly ILogger<ScrimMatchDataService> _logger;

    public string CurrentMatchId { get ; set; }
    public int CurrentMatchRound { get; set; } = 0;
    public int CurrentMatchRulesetId { get; set; }

    private readonly KeyedSemaphoreSlim _scrimMatchLock = new();
    private readonly KeyedSemaphoreSlim _scrimMatchRoundConfigurationLock = new();
    private readonly KeyedSemaphoreSlim _scrimMatchParticipatingPlayerLock = new();

    public ScrimMatchDataService(IDbContextFactory<PlanetmansDbContext> dbContextFactory, ILogger<ScrimMatchDataService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    IEnumerable<Data.Models.ScrimMatch> IScrimMatchDataService.GetAllMatches()
    {
        throw new NotImplementedException();
    }

    public async Task<Data.Models.ScrimMatch?> GetCurrentMatchAsync(CancellationToken ct = default)
    {
        string matchId = CurrentMatchId;

        using (await _scrimMatchLock.WaitAsync(matchId, ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                return await dbContext.ScrimMatches.FirstOrDefaultAsync(sm => sm.Id == matchId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current match");
                return null;
            }
        }
    }

    public async Task SaveToCurrentMatchAsync(Data.Models.ScrimMatch scrimMatch, CancellationToken ct = default)
    {
        string id = scrimMatch.Id;

        using (await _scrimMatchLock.WaitAsync(id, ct))
        {
            string oldMatchId = CurrentMatchId;

            CurrentMatchId = id;

            try
            {
                CurrentMatchId = id;

                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                Data.Models.ScrimMatch? storeEntity = await dbContext.ScrimMatches
                    .FirstOrDefaultAsync(sm => sm.Id == id, ct);

                if (storeEntity == null)
                {
                    dbContext.ScrimMatches.Add(scrimMatch);
                }
                else
                {
                    storeEntity = scrimMatch;
                    dbContext.ScrimMatches.Update(storeEntity);
                }

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                CurrentMatchId = oldMatchId;

                _logger.LogError(ex, "Failed to save the current match");
            }
        }
    }

    public async Task SaveCurrentMatchRoundConfigurationAsync
    (
        MatchConfiguration matchConfiguration,
        CancellationToken ct = default
    )
    {
        string matchId = CurrentMatchId;
        int round = CurrentMatchRound;

        using (await _scrimMatchRoundConfigurationLock.WaitAsync($"{matchId}_{round}", ct))
        {
            if (round <= 0)
            {
                return;
            }

            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                Data.Models.ScrimMatch? matchEntity = await dbContext.ScrimMatches
                    .FirstOrDefaultAsync(sm => sm.Id == matchId, ct);

                if (matchEntity == null)
                {
                    return;
                }

                ScrimMatchRoundConfiguration? storeEntity = await dbContext.ScrimMatchRoundConfigurations
                    .Where(rc => rc.ScrimMatchId == matchId && rc.ScrimMatchRound == round)
                    .FirstOrDefaultAsync(ct);

                if (storeEntity == null)
                {
                    dbContext.ScrimMatchRoundConfigurations.Add(ConvertToDbModel(matchConfiguration));
                }
                else
                {
                    storeEntity = ConvertToDbModel(matchConfiguration);
                    dbContext.ScrimMatchRoundConfigurations.Update(storeEntity);
                }

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save a match round configuration");
            }
        }
    }

    public async Task RemoveMatchRoundConfigurationAsync(int roundToDelete, CancellationToken ct = default)
    {
        string matchId = CurrentMatchId;

        using (await _scrimMatchRoundConfigurationLock.WaitAsync($"{matchId}_{roundToDelete}", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                ScrimMatchRoundConfiguration? storeEntity = await dbContext.ScrimMatchRoundConfigurations
                    .Where(rc => rc.ScrimMatchId == matchId && rc.ScrimMatchRound == roundToDelete)
                    .FirstOrDefaultAsync(ct);

                if (storeEntity == null)
                    return;

                dbContext.ScrimMatchRoundConfigurations.Remove(storeEntity);
                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove a match round configuration ({RoundId})", roundToDelete);
            }
        }
    }

    private ScrimMatchRoundConfiguration ConvertToDbModel(MatchConfiguration matchConfiguration)
    {
        return new ScrimMatchRoundConfiguration
        {
            ScrimMatchId = CurrentMatchId,
            ScrimMatchRound = CurrentMatchRound,
            Title = matchConfiguration.Title,
            RoundSecondsTotal = matchConfiguration.RoundSecondsTotal,
            WorldId = matchConfiguration.WorldId,
            IsManualWorldId = matchConfiguration.IsManualWorldId,
            FacilityId = matchConfiguration.FacilityId > 0 ? matchConfiguration.FacilityId : null,
            IsRoundEndedOnFacilityCapture = matchConfiguration.EndRoundOnFacilityCapture
        };
    }

    public async Task SaveMatchParticipatingPlayerAsync(Player player, CancellationToken ct = default)
    {
        string matchId = CurrentMatchId;

        using (await _scrimMatchParticipatingPlayerLock.WaitAsync($"{player.Id}_{matchId}", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                ScrimMatchParticipatingPlayer? storeEntity = await dbContext.ScrimMatchParticipatingPlayers
                    .Where(p => p.CharacterId == player.Id && p.ScrimMatchId == matchId)
                    .FirstOrDefaultAsync(ct);

                if (storeEntity == null)
                {
                    dbContext.ScrimMatchParticipatingPlayers.Add(ConvertToDbModel(player, matchId));
                }
                else
                {
                    storeEntity = ConvertToDbModel(player, matchId);
                    dbContext.ScrimMatchParticipatingPlayers.Update(storeEntity);
                }

                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save a match player ({PlayerId})", player.Id);
            }
        }
    }

    public async Task<bool> TryRemoveMatchParticipatingPlayerAsync(ulong characterId, CancellationToken ct = default)
    {
        string matchId = CurrentMatchId;

        using (await _scrimMatchParticipatingPlayerLock.WaitAsync($"{characterId}_{matchId}", ct))
        {
            try
            {
                await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

                ScrimMatchParticipatingPlayer? storeEntity = await dbContext.ScrimMatchParticipatingPlayers
                    .Where(p => p.CharacterId == characterId && p.ScrimMatchId == matchId)
                    .FirstOrDefaultAsync(ct);

                if (storeEntity == null)
                    return false;


                dbContext.ScrimMatchParticipatingPlayers.Remove(storeEntity);
                await dbContext.SaveChangesAsync(ct);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError
                (
                    ex,
                    "Failed to remove player {PlayerId} from match {MatchId}",
                    characterId,
                    matchId
                );

                return false;
            }
        }
    }

    private static ScrimMatchParticipatingPlayer ConvertToDbModel(Player player, string matchId)
        => new()
        {
            ScrimMatchId = matchId,
            CharacterId = player.Id,
            TeamOrdinal = player.TeamOrdinal,
            NameDisplay = player.NameDisplay,
            NameFull = player.NameFull,
            FactionId = player.FactionId,
            PrestigeLevel = player.PrestigeLevel,
            IsFromOutfit = player.IsOutfitless,
            OutfitId = player.IsOutfitless ? null : player.OutfitId,
            IsFromConstructedTeam = player.IsFromConstructedTeam,
            ConstructedTeamId = player.IsFromConstructedTeam ? player.ConstructedTeamId : null
        };
}
