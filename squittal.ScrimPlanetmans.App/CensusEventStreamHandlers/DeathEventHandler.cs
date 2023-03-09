using System;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.Planetside;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusEventStreamHandlers;

public class DeathEventHandler : IPayloadHandler<IDeath>
{
    private readonly ILogger<DeathEventHandler> _logger;
    private readonly IEventFilterService _eventFilter;
    private readonly IItemService _itemService;
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchScorer _scorer;
    private readonly IScrimMatchDataService _scrimMatchService;
    private readonly PlanetmansDbContext _dbContext;

    public DeathEventHandler
    (
        ILogger<DeathEventHandler> logger,
        IEventFilterService eventFilter,
        IItemService itemService,
        IScrimTeamsManager teamsManager,
        IScrimMessageBroadcastService messageService,
        IScrimMatchScorer scorer,
        IScrimMatchDataService scrimMatchService,
        PlanetmansDbContext dbContext
    )
    {
        _logger = logger;
        _eventFilter = eventFilter;
        _itemService = itemService;
        _teamsManager = teamsManager;
        _messageService = messageService;
        _scorer = scorer;
        _scrimMatchService = scrimMatchService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task HandleAsync(IDeath payload, CancellationToken ct = default)
    {
        // Sanity check
        if (payload.CharacterID is 0)
            return;

        bool involvesBenchedPlayer = false;
        ulong attackerId = payload.AttackerCharacterID;
        ulong victimId = payload.CharacterID;
        bool isSuicide = attackerId is 0 || attackerId == victimId;

        Player? victimPlayer = _teamsManager.GetPlayerFromId(victimId);
        Player? attackerPlayer = _teamsManager.GetPlayerFromId(attackerId);

        // No point broadcasting a death event for a victim we're not tracking
        if (victimPlayer is null)
            return;

        ScrimActionWeaponInfo weapon;
        Item? weaponItem = await _itemService.GetWeaponItemAsync((int)payload.AttackerWeaponID);

        if (weaponItem is not null)
        {
            weapon = new ScrimActionWeaponInfo
            (
                weaponItem.Id,
                weaponItem.ItemCategoryId,
                weaponItem.Name ?? "Unknown weapon",
                weaponItem.IsVehicleWeapon
            );
        }
        else
        {
            weapon = new ScrimActionWeaponInfo((int)payload.AttackerWeaponID, null, null, null);
        }

        ScrimDeathActionEvent deathEvent = new()
        {
            VictimCharacterId = victimId,
            AttackerCharacterId = isSuicide ? victimId : attackerId,
            VictimPlayer = victimPlayer,
            AttackerPlayer = isSuicide ? victimPlayer : attackerPlayer,
            VictimLoadoutId = (int)payload.CharacterLoadoutID,
            AttackerLoadoutId = (int)(isSuicide ? payload.CharacterLoadoutID : payload.AttackerLoadoutID),
            Timestamp = payload.Timestamp.UtcDateTime,
            ZoneId = (int)payload.ZoneID.CombinedId,
            IsHeadshot = payload.IsHeadshot,
            Weapon = weapon
        };

        try
        {
            _teamsManager.SetPlayerLoadoutId(victimId, (int)payload.CharacterLoadoutID);
            involvesBenchedPlayer = involvesBenchedPlayer || victimPlayer.IsBenched;

            if (attackerPlayer is not null)
            {
                _teamsManager.SetPlayerLoadoutId(attackerId, (int)payload.AttackerLoadoutID);
                involvesBenchedPlayer = involvesBenchedPlayer || attackerPlayer.IsBenched;
            }

            deathEvent.ActionType = GetDeathScrimActionType(deathEvent);
            if (deathEvent.ActionType != ScrimActionType.OutsideInterference)
            {
                deathEvent.DeathType = GetDeathEventType(deathEvent.ActionType);

                if (_eventFilter.IsScoringEnabled && !involvesBenchedPlayer)
                {
                    ScrimEventScoringResult scoringResult = await _scorer.ScoreDeathEventAsync(deathEvent, ct);
                    deathEvent.Points = scoringResult.Points;
                    deathEvent.IsBanned = scoringResult.IsBanned;

                    string currentMatchId = _scrimMatchService.CurrentMatchId;
                    int currentRound = _scrimMatchService.CurrentMatchRound;

                    if (_eventFilter.IsEventStoringEnabled && !string.IsNullOrWhiteSpace(currentMatchId))
                    {
                        ScrimDeath dataModel = new()
                        {
                            ScrimMatchId = currentMatchId,
                            Timestamp = deathEvent.Timestamp,
                            AttackerCharacterId = attackerId,
                            VictimCharacterId = victimId,
                            ScrimMatchRound = currentRound,
                            ActionType = deathEvent.ActionType,
                            DeathType = deathEvent.DeathType,
                            ZoneId = (int)deathEvent.ZoneId,
                            WorldId = (int)payload.WorldID,
                            AttackerTeamOrdinal = deathEvent.AttackerPlayer?.TeamOrdinal,
                            AttackerFactionId = deathEvent.AttackerPlayer?.FactionId,
                            AttackerNameFull = deathEvent.AttackerPlayer?.NameFull,
                            AttackerLoadoutId = deathEvent.AttackerPlayer?.LoadoutId,
                            AttackerOutfitId = deathEvent.AttackerPlayer?.OutfitId,
                            AttackerOutfitAlias = deathEvent.AttackerPlayer?.OutfitAlias,
                            AttackerIsOutfitless = deathEvent.AttackerPlayer?.IsOutfitless,
                            VictimTeamOrdinal = deathEvent.VictimPlayer.TeamOrdinal,
                            VictimFactionId = deathEvent.VictimPlayer.FactionId,
                            VictimNameFull = deathEvent.VictimPlayer.NameFull,
                            VictimLoadoutId = deathEvent.VictimLoadoutId,
                            VictimOutfitId = deathEvent.VictimPlayer.IsOutfitless ? null : deathEvent.VictimPlayer.OutfitId,
                            VictimOutfitAlias = deathEvent.VictimPlayer.IsOutfitless ? null : deathEvent.VictimPlayer.OutfitAlias,
                            VictimIsOutfitless = deathEvent.VictimPlayer.IsOutfitless,
                            WeaponId = deathEvent.Weapon.Id,
                            WeaponItemCategoryId = deathEvent.Weapon.ItemCategoryId,
                            IsVehicleWeapon = deathEvent.Weapon.IsVehicleWeapon,
                            AttackerVehicleId = deathEvent.AttackerVehicleId,
                            IsHeadshot = deathEvent.IsHeadshot,
                            Points = deathEvent.Points,
                            //AttackerResultingPoints = deathEvent.AttackerPlayer.EventAggregate.Points,
                            //AttackerResultingNetScore = deathEvent.AttackerPlayer.EventAggregate.NetScore,
                            //VictimResultingPoints = deathEvent.VictimPlayer.EventAggregate.Points,
                            //VictimResultingNetScore = deathEvent.VictimPlayer.EventAggregate.NetScore
                        };

                        _dbContext.ScrimDeaths.Add(dataModel);
                        await _dbContext.SaveChangesAsync(ct);
                    }
                }
            }

            _messageService.BroadcastScrimDeathActionEventMessage(new ScrimDeathActionEventMessage(deathEvent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process a death event");
        }
    }

    private ScrimActionType GetDeathScrimActionType(ScrimDeathActionEvent death)
    {
        // Determine if this is involves a non-tracked player
        if (death.AttackerPlayer is null)
            return ScrimActionType.OutsideInterference;

        bool attackerIsVehicle = death.Weapon is { IsVehicleWeapon: true };
        bool attackerIsMax = ProfileService.IsMaxLoadoutId(death.AttackerLoadoutId);
        bool victimIsMax = ProfileService.IsMaxLoadoutId(death.VictimLoadoutId);
        bool sameTeam = _teamsManager.DoPlayersShareTeam(death.AttackerPlayer, death.VictimPlayer);
        bool samePlayer = (death.AttackerPlayer == death.VictimPlayer || death.AttackerPlayer == null);

        if (samePlayer)
        {
            return victimIsMax
                ? ScrimActionType.MaxSuicide
                : ScrimActionType.InfantrySuicide;
        }

        if (sameTeam)
        {
            if (attackerIsVehicle)
            {
                return victimIsMax
                    ? ScrimActionType.VehicleTeamkillMax
                    : ScrimActionType.VehicleTeamkillInfantry;
            }

            if (attackerIsMax)
            {
                return victimIsMax
                    ? ScrimActionType.MaxTeamkillMax
                    : ScrimActionType.MaxTeamkillInfantry;
            }

            return victimIsMax
                ? ScrimActionType.InfantryTeamkillMax
                : ScrimActionType.InfantryTeamkillInfantry;
        }

        if (attackerIsVehicle)
        {
            return victimIsMax
                ? ScrimActionType.VehicleKillMax
                : ScrimActionType.VehicleKillInfantry;
        }

        if (attackerIsMax)
        {
            return victimIsMax
                ? ScrimActionType.MaxKillMax
                : ScrimActionType.MaxKillInfantry;
        }

        return victimIsMax
            ? ScrimActionType.InfantryKillMax
            : ScrimActionType.InfantryKillInfantry;
    }

    private static DeathEventType GetDeathEventType(ScrimActionType scrimActionType)
    {
        return scrimActionType switch
        {
            ScrimActionType.MaxSuicide => DeathEventType.Suicide,
            ScrimActionType.InfantrySuicide => DeathEventType.Suicide,
            ScrimActionType.MaxTeamkillMax => DeathEventType.Teamkill,
            ScrimActionType.MaxTeamkillInfantry => DeathEventType.Teamkill,
            ScrimActionType.InfantryTeamkillMax => DeathEventType.Teamkill,
            ScrimActionType.InfantryTeamkillInfantry => DeathEventType.Teamkill,
            ScrimActionType.VehicleTeamkillMax => DeathEventType.Teamkill,
            ScrimActionType.VehicleTeamkillInfantry => DeathEventType.Teamkill,
            ScrimActionType.MaxKillMax => DeathEventType.Kill,
            ScrimActionType.MaxKillInfantry => DeathEventType.Kill,
            ScrimActionType.InfantryKillMax => DeathEventType.Kill,
            ScrimActionType.InfantryKillInfantry => DeathEventType.Kill,
            ScrimActionType.VehicleKillMax => DeathEventType.Kill,
            ScrimActionType.VehicleKillInfantry => DeathEventType.Kill,
            _ => DeathEventType.Kill
        };
    }
}
