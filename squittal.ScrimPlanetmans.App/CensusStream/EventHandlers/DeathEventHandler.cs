using System;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using DbgCensus.EventStream.EventHandlers.Abstractions;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusStream.Interfaces;
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

namespace squittal.ScrimPlanetmans.App.CensusStream.EventHandlers;

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
        bool involvesBenchedPlayer = false;
        string attackerId = payload.AttackerCharacterID.ToString();
        string victimId = payload.CharacterID.ToString();

        ScrimActionWeaponInfo weapon;
        Item? weaponItem = await _itemService.GetWeaponItemAsync((int)payload.AttackerWeaponID);

        if (weaponItem is not null)
        {
            weapon = new ScrimActionWeaponInfo
            {
                Id = weaponItem.Id,
                ItemCategoryId = weaponItem.ItemCategoryId,
                Name = weaponItem.Name ?? "Unknown weapon",
                IsVehicleWeapon = weaponItem.IsVehicleWeapon
            };
        }
        else
        {
            weapon = new ScrimActionWeaponInfo
            {
                Id = (int)payload.AttackerWeaponID
            };
        }

        ScrimDeathActionEvent deathEvent = new()
        {
            Timestamp = payload.Timestamp.UtcDateTime,
            ZoneId = (int)payload.ZoneID.CombinedId,
            IsHeadshot = payload.IsHeadshot,
            Weapon = weapon
        };

        try
        {
            if (payload.AttackerCharacterID is not 0)
            {
                deathEvent.AttackerCharacterId = attackerId;
                deathEvent.AttackerLoadoutId = (int)payload.AttackerLoadoutID;

                Player? attackerPlayer = _teamsManager.GetPlayerFromId(attackerId);
                deathEvent.AttackerPlayer = attackerPlayer;

                if (attackerPlayer is not null)
                {
                    _teamsManager.SetPlayerLoadoutId(attackerId, deathEvent.AttackerLoadoutId);
                    involvesBenchedPlayer = involvesBenchedPlayer || attackerPlayer.IsBenched;
                }
            }

            if (payload.CharacterID is not 0)
            {
                deathEvent.VictimCharacterId = victimId;
                deathEvent.VictimLoadoutId = (int)payload.CharacterLoadoutID;

                Player? victimPlayer = _teamsManager.GetPlayerFromId(victimId);
                deathEvent.VictimPlayer = victimPlayer;

                if (victimPlayer is not null)
                {
                    _teamsManager.SetPlayerLoadoutId(victimId, deathEvent.VictimLoadoutId);
                    involvesBenchedPlayer = involvesBenchedPlayer || victimPlayer.IsBenched;
                }
            }

            deathEvent.ActionType = GetDeathScrimActionType(deathEvent);

            if (deathEvent.ActionType != ScrimActionType.OutsideInterference)
            {
                deathEvent.DeathType = GetDeathEventType(deathEvent.ActionType);

                if (deathEvent.DeathType == DeathEventType.Suicide)
                {
                    deathEvent.AttackerPlayer = deathEvent.VictimPlayer;
                    deathEvent.AttackerCharacterId = deathEvent.VictimCharacterId;
                    deathEvent.AttackerLoadoutId = deathEvent.VictimLoadoutId;
                }

                if (_eventFilter.IsScoringEnabled && !involvesBenchedPlayer)
                {
                    var scoringResult = await _scorer.ScoreDeathEventAsync(deathEvent, ct);
                    deathEvent.Points = scoringResult.Points;
                    deathEvent.IsBanned = scoringResult.IsBanned;

                    var currentMatchId = _scrimMatchService.CurrentMatchId;
                    var currentRound = _scrimMatchService.CurrentMatchRound;

                    if (_eventFilter.IsEventStoringEnabled && !string.IsNullOrWhiteSpace(currentMatchId))
                    {
                        var dataModel = new ScrimDeath
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
                            AttackerTeamOrdinal = deathEvent.AttackerPlayer.TeamOrdinal,
                            AttackerFactionId = deathEvent.AttackerPlayer.FactionId,
                            AttackerNameFull = deathEvent.AttackerPlayer.NameFull,
                            AttackerLoadoutId = deathEvent.AttackerPlayer.LoadoutId,
                            AttackerOutfitId = deathEvent.AttackerPlayer.IsOutfitless ? null : deathEvent.AttackerPlayer.OutfitId,
                            AttackerOutfitAlias = deathEvent.AttackerPlayer.IsOutfitless ? null : deathEvent.AttackerPlayer.OutfitAlias,
                            AttackerIsOutfitless = deathEvent.AttackerPlayer.IsOutfitless,
                            VictimTeamOrdinal = deathEvent.VictimPlayer.TeamOrdinal,
                            VictimFactionId = deathEvent.VictimPlayer.FactionId,
                            VictimNameFull = deathEvent.VictimPlayer.NameFull,
                            VictimLoadoutId = deathEvent.VictimPlayer.LoadoutId,
                            VictimOutfitId = deathEvent.VictimPlayer.IsOutfitless ? null : deathEvent.VictimPlayer.OutfitId,
                            VictimOutfitAlias = deathEvent.VictimPlayer.IsOutfitless ? null : deathEvent.VictimPlayer.OutfitAlias,
                            VictimIsOutfitless = deathEvent.VictimPlayer.IsOutfitless,
                            WeaponId = deathEvent.Weapon?.Id,
                            WeaponItemCategoryId = deathEvent.Weapon?.ItemCategoryId,
                            IsVehicleWeapon = deathEvent.Weapon?.IsVehicleWeapon,
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
        if ((death.AttackerPlayer == null && !string.IsNullOrWhiteSpace(death.AttackerCharacterId))
            || (death.VictimPlayer == null && !string.IsNullOrWhiteSpace(death.VictimCharacterId)))
        {
            return ScrimActionType.OutsideInterference;
        }

        bool attackerIsVehicle = death.Weapon is { IsVehicleWeapon: true };
        bool attackerIsMax = death.AttackerLoadoutId != null && ProfileService.IsMaxLoadoutId(death.AttackerLoadoutId);
        bool victimIsMax = death.VictimLoadoutId != null && ProfileService.IsMaxLoadoutId(death.VictimLoadoutId);
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
