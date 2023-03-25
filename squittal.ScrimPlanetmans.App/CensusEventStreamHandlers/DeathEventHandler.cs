using System;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.CensusEventStreamHandlers;

public class DeathEventHandler : IPayloadHandler<IDeath>
{
    private readonly ILogger<DeathEventHandler> _logger;
    private readonly IEventFilterService _eventFilter;
    private readonly ICensusItemService _itemService;
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchScorer _scorer;
    private readonly IScrimMatchDataService _scrimMatchService;
    private readonly ILoadoutService _loadoutService;
    private readonly PlanetmansDbContext _dbContext;

    public DeathEventHandler
    (
        ILogger<DeathEventHandler> logger,
        IEventFilterService eventFilter,
        ICensusItemService itemService,
        IScrimTeamsManager teamsManager,
        IScrimMessageBroadcastService messageService,
        IScrimMatchScorer scorer,
        IScrimMatchDataService scrimMatchService,
        ILoadoutService loadoutService,
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
        _loadoutService = loadoutService;
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
        CensusItem? weaponItem = await _itemService.GetWeaponAsync(payload.AttackerWeaponID, ct);

        if (weaponItem is not null)
        {
            weapon = new ScrimActionWeaponInfo
            (
                weaponItem.ItemId,
                weaponItem.ItemCategoryId,
                weaponItem.Name.English.HasValue ? weaponItem.Name.English.Value : "Unknown weapon",
                weaponItem.IsVehicleWeapon
            );
        }
        else
        {
            weapon = new ScrimActionWeaponInfo(payload.AttackerWeaponID, null, null, null);
        }

        ScrimDeathActionEvent deathEvent = new()
        {
            VictimCharacterId = victimId,
            AttackerCharacterId = isSuicide ? victimId : attackerId,
            VictimPlayer = victimPlayer,
            AttackerPlayer = isSuicide ? victimPlayer : attackerPlayer,
            VictimLoadoutId = payload.CharacterLoadoutID,
            AttackerLoadoutId = isSuicide ? payload.CharacterLoadoutID : payload.AttackerLoadoutID,
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

            deathEvent.ActionType = await GetDeathScrimActionType(deathEvent, ct);
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
                            AttackerFactionId = attackerPlayer?.FactionId,
                            AttackerLoadoutId = deathEvent.AttackerLoadoutId,
                            VictimCharacterId = victimId,
                            ScrimMatchRound = currentRound,
                            ActionType = deathEvent.ActionType,
                            DeathType = deathEvent.DeathType,
                            AttackerTeamOrdinal = deathEvent.AttackerPlayer?.TeamOrdinal,
                            VictimTeamOrdinal = deathEvent.VictimPlayer.TeamOrdinal,
                            IsHeadshot = deathEvent.IsHeadshot,
                            Points = deathEvent.Points,
                            WeaponId = deathEvent.Weapon.Id,
                            AttackerVehicleId = deathEvent.AttackerVehicleId
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

    private async Task<ScrimActionType> GetDeathScrimActionType(ScrimDeathActionEvent death, CancellationToken ct)
    {
        // Determine if this is involves a non-tracked player
        if (death.AttackerPlayer is null)
            return ScrimActionType.OutsideInterference;

        bool attackerIsVehicle = death.Weapon is { IsVehicleWeapon: true };
        bool attackerIsMax = await _loadoutService.IsLoadoutOfProfileTypeAsync
        (
            death.AttackerLoadoutId,
            CensusProfileType.MAX,
            ct
        );
        bool victimIsMax = await _loadoutService.IsLoadoutOfProfileTypeAsync
        (
            death.VictimLoadoutId,
            CensusProfileType.MAX,
            ct
        );
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
