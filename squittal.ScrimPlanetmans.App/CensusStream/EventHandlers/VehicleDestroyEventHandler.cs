﻿using System;
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

public class VehicleDestroyEventHandler : IPayloadHandler<IVehicleDestroy>
{
    private readonly ILogger<VehicleDestroyEventHandler> _logger;
    private readonly IEventFilterService _eventFilter;
    private readonly IItemService _itemService;
    private readonly IVehicleService _vehicleService;
    private readonly IScrimTeamsManager _teamsManager;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly IScrimMatchScorer _scorer;
    private readonly IScrimMatchDataService _scrimMatchService;
    private readonly PlanetmansDbContext _dbContext;

    public VehicleDestroyEventHandler
    (
        ILogger<VehicleDestroyEventHandler> logger,
        IEventFilterService eventFilter,
        IItemService itemService,
        IVehicleService vehicleService,
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
        _vehicleService = vehicleService;
        _teamsManager = teamsManager;
        _messageService = messageService;
        _scorer = scorer;
        _scrimMatchService = scrimMatchService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task HandleAsync(IVehicleDestroy payload, CancellationToken ct = new())
    {
        string attackerId = payload.AttackerCharacterID.ToString();
        string victimId = payload.CharacterID.ToString();
        bool involvesBenchedPlayer = false;

        // Don't bother tracking players destroying unclaimed vehicles
        if (payload.CharacterID is 0)
            return;

        ScrimActionWeaponInfo weapon;
        Item? weaponItem = await _itemService.GetWeaponItemAsync((int)payload.AttackerWeaponID);

        if (weaponItem != null)
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
            weapon = new ScrimActionWeaponInfo()
            {
                Id = (int)payload.AttackerWeaponID
            };
        }

        ScrimVehicleDestructionActionEvent destructionEvent = new()
        {
            Timestamp = payload.Timestamp.UtcDateTime,
            ZoneId = (int)payload.ZoneID.CombinedId,
            Weapon = weapon
        };

        Vehicle? attackerVehicle = await _vehicleService.GetVehicleInfoAsync((int)payload.AttackerVehicleID);
        if (attackerVehicle != null)
        {
            destructionEvent.AttackerVehicle = new ScrimActionVehicleInfo(attackerVehicle);
        }

        Vehicle? victimVehicle = await _vehicleService.GetVehicleInfoAsync((int)payload.VehicleID);
        if (victimVehicle != null)
        {
            destructionEvent.VictimVehicle = new ScrimActionVehicleInfo(victimVehicle);
        }

        try
        {
            if (payload.AttackerCharacterID is not 0)
            {
                destructionEvent.AttackerCharacterId = attackerId;
                destructionEvent.AttackerLoadoutId = (int)payload.AttackerLoadoutID;

                Player? attackerPlayer = _teamsManager.GetPlayerFromId(attackerId);
                destructionEvent.AttackerPlayer = attackerPlayer;

                if (attackerPlayer != null)
                {
                    _teamsManager.SetPlayerLoadoutId(attackerId, destructionEvent.AttackerLoadoutId);
                    involvesBenchedPlayer = involvesBenchedPlayer || attackerPlayer.IsBenched;
                }
            }

            destructionEvent.VictimCharacterId = victimId;
            Player? victimPlayer = _teamsManager.GetPlayerFromId(victimId);
            destructionEvent.VictimPlayer = victimPlayer;

            if (victimPlayer != null)
                involvesBenchedPlayer = involvesBenchedPlayer || victimPlayer.IsBenched;

            destructionEvent.DeathType = GetVehicleDestructionDeathType(destructionEvent);
            destructionEvent.ActionType = GetVehicleDestructionScrimActionType(destructionEvent);

            if (destructionEvent.ActionType is not ScrimActionType.OutsideInterference)
            {
                if (destructionEvent.DeathType is DeathEventType.Suicide)
                {
                    destructionEvent.AttackerPlayer = destructionEvent.VictimPlayer;
                    destructionEvent.AttackerCharacterId = destructionEvent.VictimCharacterId;
                    destructionEvent.AttackerVehicle = destructionEvent.VictimVehicle;
                }

                if (_eventFilter.IsScoringEnabled && !involvesBenchedPlayer)
                {
                    ScrimEventScoringResult scoringResult = await _scorer.ScoreVehicleDestructionEventAsync(destructionEvent, ct);
                    destructionEvent.Points = scoringResult.Points;
                    destructionEvent.IsBanned = scoringResult.IsBanned;

                    string currentMatchId = _scrimMatchService.CurrentMatchId;
                    int currentRound = _scrimMatchService.CurrentMatchRound;

                    if (_eventFilter.IsEventStoringEnabled && !string.IsNullOrWhiteSpace(currentMatchId))
                    {
                        ScrimVehicleDestruction dataModel = new()
                        {
                            ScrimMatchId = currentMatchId,
                            Timestamp = destructionEvent.Timestamp,
                            AttackerCharacterId = attackerId,
                            VictimCharacterId = victimId,
                            VictimVehicleId = destructionEvent.VictimVehicle?.Id ?? (int)payload.VehicleID,
                            AttackerVehicleId = destructionEvent.AttackerVehicle?.Id,
                            ScrimMatchRound = currentRound,
                            ActionType = destructionEvent.ActionType,
                            DeathType = destructionEvent.DeathType,
                            AttackerVehicleClass = destructionEvent.AttackerVehicle?.Type,
                            VictimVehicleClass = destructionEvent.VictimVehicle?.Type,
                            AttackerTeamOrdinal = destructionEvent.AttackerPlayer.TeamOrdinal,
                            VictimTeamOrdinal = destructionEvent.VictimPlayer.TeamOrdinal,
                            AttackerFactionId = destructionEvent.AttackerPlayer.FactionId,
                            AttackerNameFull = destructionEvent.AttackerPlayer.NameFull,
                            AttackerLoadoutId = destructionEvent.AttackerPlayer?.LoadoutId,
                            AttackerOutfitId = destructionEvent.AttackerPlayer.IsOutfitless ? null : destructionEvent.AttackerPlayer.OutfitId,
                            AttackerOutfitAlias = destructionEvent.AttackerPlayer.IsOutfitless ? null : destructionEvent.AttackerPlayer.OutfitAlias,
                            AttackerIsOutfitless = destructionEvent.AttackerPlayer.IsOutfitless,
                            VictimFactionId = destructionEvent.VictimPlayer.FactionId,
                            VictimNameFull = destructionEvent.VictimPlayer.NameFull,
                            VictimLoadoutId = destructionEvent.VictimPlayer?.LoadoutId,
                            VictimOutfitId = destructionEvent.VictimPlayer.IsOutfitless ? null : destructionEvent.VictimPlayer.OutfitId,
                            VictimOutfitAlias = destructionEvent.VictimPlayer.IsOutfitless ? null : destructionEvent.VictimPlayer.OutfitAlias,
                            VictimIsOutfitless = destructionEvent.VictimPlayer.IsOutfitless,
                            WeaponId = destructionEvent.Weapon?.Id,
                            WeaponItemCategoryId = destructionEvent.Weapon?.ItemCategoryId,
                            IsVehicleWeapon = destructionEvent.Weapon?.IsVehicleWeapon,
                            ZoneId = (int)destructionEvent.ZoneId,
                            WorldId = (int)payload.WorldID,
                            Points = destructionEvent.Points,
                            //AttackerResultingPoints = destructionEvent.AttackerPlayer.EventAggregate.Points,
                            //AttackerResultingNetScore = destructionEvent.AttackerPlayer.EventAggregate.NetScore,
                            //VictimResultingPoints = destructionEvent.VictimPlayer.EventAggregate.Points,
                            //VictimResultingNetScore = destructionEvent.VictimPlayer.EventAggregate.NetScore
                        };

                        _dbContext.ScrimVehicleDestructions.Add(dataModel);
                        await _dbContext.SaveChangesAsync(ct);
                    }
                }
            }

            _messageService.BroadcastScrimVehicleDestructionActionEventMessage(new ScrimVehicleDestructionActionEventMessage(destructionEvent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process a VehicleDestroy event");
        }
    }

    private DeathEventType GetVehicleDestructionDeathType(ScrimVehicleDestructionActionEvent destruction)
    {
        bool sameTeam = _teamsManager.DoPlayersShareTeam(destruction.AttackerPlayer, destruction.VictimPlayer);
        bool samePlayer = destruction.AttackerPlayer == destruction.VictimPlayer || destruction.AttackerPlayer is null;

        if (samePlayer)
            return DeathEventType.Suicide;

        return sameTeam
            ? DeathEventType.Teamkill
            : DeathEventType.Kill;
    }

    private ScrimActionType GetVehicleDestructionScrimActionType(ScrimVehicleDestructionActionEvent destruction)
    {
        // TODO: determine what a bailed-then-crashed undamaged vehicle looks like
        // Determine if this is involves a non-tracked player
        if ((destruction.AttackerPlayer == null && !string.IsNullOrWhiteSpace(destruction.AttackerCharacterId))
            || (destruction.VictimPlayer == null && !string.IsNullOrWhiteSpace(destruction.VictimCharacterId)))
        {
            return ScrimActionType.OutsideInterference;
        }

        if (destruction.VictimVehicle is null)
            return ScrimActionType.Unknown;

        bool attackerIsMax = ProfileService.IsMaxLoadoutId(destruction.AttackerLoadoutId);
        bool attackerIsVehicle = destruction.Weapon is { IsVehicleWeapon: true }
            || destruction.AttackerVehicle?.Type is not VehicleType.Unknown;


        return destruction.DeathType switch
        {
            DeathEventType.Suicide => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.InterceptorSuicide,
                VehicleType.ESF => ScrimActionType.EsfSuicide,
                VehicleType.Valkyrie => ScrimActionType.ValkyrieSuicide,
                VehicleType.Liberator => ScrimActionType.LiberatorSuicide,
                VehicleType.Galaxy => ScrimActionType.GalaxySuicide,
                VehicleType.Bastion => ScrimActionType.BastionSuicide,

                VehicleType.Flash => ScrimActionType.FlashSuicide,
                VehicleType.Harasser => ScrimActionType.HarasserSuicide,
                VehicleType.ANT => ScrimActionType.AntSuicide,
                VehicleType.Sunderer => ScrimActionType.SundererSuicide,
                VehicleType.Lightning => ScrimActionType.LightningSuicide,
                VehicleType.MBT => ScrimActionType.MbtSuicide,

                _ => ScrimActionType.Unknown,
            },
            DeathEventType.Teamkill when attackerIsVehicle => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.VehicleTeamDestroyInterceptor,
                VehicleType.ESF => ScrimActionType.VehicleTeamDestroyEsf,
                VehicleType.Valkyrie => ScrimActionType.VehicleTeamDestroyValkyrie,
                VehicleType.Liberator => ScrimActionType.VehicleTeamDestroyLiberator,
                VehicleType.Galaxy => ScrimActionType.VehicleTeamDestroyGalaxy,
                VehicleType.Bastion => ScrimActionType.VehicleTeamDestroyBastion,

                VehicleType.Flash => ScrimActionType.VehicleTeamDestroyFlash,
                VehicleType.Harasser => ScrimActionType.VehicleTeamDestroyHarasser,
                VehicleType.ANT => ScrimActionType.VehicleTeamDestroyAnt,
                VehicleType.Sunderer => ScrimActionType.VehicleTeamDestroySunderer,
                VehicleType.Lightning => ScrimActionType.VehicleTeamDestroyLightning,
                VehicleType.MBT => ScrimActionType.VehicleTeamDestroyMbt,

                _ => ScrimActionType.Unknown,
            },
            DeathEventType.Teamkill when attackerIsMax => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.MaxTeamDestroyInterceptor,
                VehicleType.ESF => ScrimActionType.MaxTeamDestroyEsf,
                VehicleType.Valkyrie => ScrimActionType.MaxTeamDestroyValkyrie,
                VehicleType.Liberator => ScrimActionType.MaxTeamDestroyLiberator,
                VehicleType.Galaxy => ScrimActionType.MaxTeamDestroyGalaxy,
                VehicleType.Bastion => ScrimActionType.MaxTeamDestroyBastion,

                VehicleType.Flash => ScrimActionType.MaxTeamDestroyFlash,
                VehicleType.Harasser => ScrimActionType.MaxTeamDestroyHarasser,
                VehicleType.ANT => ScrimActionType.MaxTeamDestroyAnt,
                VehicleType.Sunderer => ScrimActionType.MaxTeamDestroySunderer,
                VehicleType.Lightning => ScrimActionType.MaxTeamDestroyLightning,
                VehicleType.MBT => ScrimActionType.MaxTeamDestroyMbt,

                _ => ScrimActionType.Unknown,
            },
            DeathEventType.Teamkill => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.InfantryTeamDestroyInterceptor,
                VehicleType.ESF => ScrimActionType.InfantryTeamDestroyEsf,
                VehicleType.Valkyrie => ScrimActionType.InfantryTeamDestroyValkyrie,
                VehicleType.Liberator => ScrimActionType.InfantryTeamDestroyLiberator,
                VehicleType.Galaxy => ScrimActionType.InfantryTeamDestroyGalaxy,
                VehicleType.Bastion => ScrimActionType.InfantryTeamDestroyBastion,

                VehicleType.Flash => ScrimActionType.InfantryTeamDestroyFlash,
                VehicleType.Harasser => ScrimActionType.InfantryTeamDestroyHarasser,
                VehicleType.ANT => ScrimActionType.InfantryTeamDestroyAnt,
                VehicleType.Sunderer => ScrimActionType.InfantryTeamDestroySunderer,
                VehicleType.Lightning => ScrimActionType.InfantryTeamDestroyLightning,
                VehicleType.MBT => ScrimActionType.InfantryTeamDestroyMbt,

                _ => ScrimActionType.Unknown,
            },
            DeathEventType.Kill when attackerIsVehicle => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.VehicleDestroyInterceptor,
                VehicleType.ESF => ScrimActionType.VehicleDestroyEsf,
                VehicleType.Valkyrie => ScrimActionType.VehicleDestroyValkyrie,
                VehicleType.Liberator => ScrimActionType.VehicleDestroyLiberator,
                VehicleType.Galaxy => ScrimActionType.VehicleDestroyGalaxy,
                VehicleType.Bastion => ScrimActionType.VehicleDestroyBastion,

                VehicleType.Flash => ScrimActionType.VehicleDestroyFlash,
                VehicleType.Harasser => ScrimActionType.VehicleDestroyHarasser,
                VehicleType.ANT => ScrimActionType.VehicleDestroyAnt,
                VehicleType.Sunderer => ScrimActionType.VehicleDestroySunderer,
                VehicleType.Lightning => ScrimActionType.VehicleDestroyLightning,
                VehicleType.MBT => ScrimActionType.VehicleDestroyMbt,

                _ => ScrimActionType.Unknown,
            },
            DeathEventType.Kill when attackerIsMax => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.MaxDestroyInterceptor,
                VehicleType.ESF => ScrimActionType.MaxDestroyEsf,
                VehicleType.Valkyrie => ScrimActionType.MaxDestroyValkyrie,
                VehicleType.Liberator => ScrimActionType.MaxDestroyLiberator,
                VehicleType.Galaxy => ScrimActionType.MaxDestroyGalaxy,
                VehicleType.Bastion => ScrimActionType.MaxDestroyBastion,

                VehicleType.Flash => ScrimActionType.MaxDestroyFlash,
                VehicleType.Harasser => ScrimActionType.MaxDestroyHarasser,
                VehicleType.ANT => ScrimActionType.MaxDestroyAnt,
                VehicleType.Sunderer => ScrimActionType.MaxDestroySunderer,
                VehicleType.Lightning => ScrimActionType.MaxDestroyLightning,
                VehicleType.MBT => ScrimActionType.MaxDestroyMbt,

                _ => ScrimActionType.Unknown,
            },
            DeathEventType.Kill => destruction.VictimVehicle.Type switch
            {
                VehicleType.Interceptor => ScrimActionType.InfantryDestroyInterceptor,
                VehicleType.ESF => ScrimActionType.InfantryDestroyEsf,
                VehicleType.Valkyrie => ScrimActionType.InfantryDestroyValkyrie,
                VehicleType.Liberator => ScrimActionType.InfantryDestroyLiberator,
                VehicleType.Galaxy => ScrimActionType.InfantryDestroyGalaxy,
                VehicleType.Bastion => ScrimActionType.InfantryDestroyBastion,

                VehicleType.Flash => ScrimActionType.InfantryDestroyFlash,
                VehicleType.Harasser => ScrimActionType.InfantryDestroyHarasser,
                VehicleType.ANT => ScrimActionType.InfantryDestroyAnt,
                VehicleType.Sunderer => ScrimActionType.InfantryDestroySunderer,
                VehicleType.Lightning => ScrimActionType.InfantryDestroyLightning,
                VehicleType.MBT => ScrimActionType.InfantryDestroyMbt,

                _ => ScrimActionType.Unknown,
            },
            _ => ScrimActionType.Unknown
        };
    }
}
