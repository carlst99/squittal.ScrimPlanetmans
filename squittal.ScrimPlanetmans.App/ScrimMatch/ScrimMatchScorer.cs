using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch;

public sealed class ScrimMatchScorer : IScrimMatchScorer
{
    private readonly IScrimRulesetManager _rulesets;
    private readonly IScrimTeamsManager _teamsManager;
    private readonly ILoadoutService _loadoutService;

    private Ruleset.Models.Ruleset? _activeRuleset => _rulesets.ActiveRuleset;

    public ScrimMatchScorer
    (
        IScrimRulesetManager rulesets,
        IScrimTeamsManager teamsManager,
        ILoadoutService loadoutService
    )
    {
        _rulesets = rulesets;
        _teamsManager = teamsManager;
        _loadoutService = loadoutService;

    }

    #region Death Events

    public async Task<ScrimEventScoringResult> ScoreDeathEventAsync
    (
        ScrimDeathActionEvent death,
        CancellationToken ct = default
    )
    {
        return death.DeathType switch
        {
            DeathEventType.Kill => await ScoreKillAsync(death, ct),
            DeathEventType.Suicide => await ScoreSuicide(death, ct),
            DeathEventType.Teamkill => await ScoreTeamkillAsync(death, ct),
            _ => new ScrimEventScoringResult(ScrimEventScorePointsSource.Default, 0, false)
        };
    }

    private async Task<ScrimEventScoringResult> ScoreKillAsync
    (
        ScrimDeathActionEvent death,
        CancellationToken ct
    )
    {
        ScrimEventScoringResult scoringResult = await GetDeathOrDestructionEventPoints
        (
            death.ActionType,
            death.Weapon.ItemCategoryId,
            death.Weapon.Id,
            death.AttackerLoadoutId,
            ct
        );

        int points = scoringResult.Points;
        int isHeadshot = death.IsHeadshot ? 1 : 0;

        ScrimEventAggregate attackerUpdate = new()
        {
            Points = points,
            NetScore = points,
            Kills = 1,
            Headshots = isHeadshot
        };

        ScrimEventAggregate victimUpdate = new()
        {
            NetScore = -points,
            Deaths = 1,
            HeadshotDeaths = isHeadshot
        };

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(death.AttackerCharacterId, attackerUpdate);
        await _teamsManager.UpdatePlayerStats(death.VictimCharacterId, victimUpdate);

        return scoringResult;
    }

    private async Task<ScrimEventScoringResult> ScoreSuicide
    (
        ScrimDeathActionEvent death,
        CancellationToken ct
    )
    {
        ScrimEventScoringResult scoringResult = await GetDeathOrDestructionEventPoints
        (
            death.ActionType,
            death.Weapon.ItemCategoryId,
            death.Weapon.Id,
            death.AttackerLoadoutId,
            ct
        );

        int points = scoringResult.Points;
        ScrimEventAggregate victimUpdate = new()
        {
            Points = points,
            NetScore = points,
            Deaths = 1,
            Suicides = 1
        };

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(death.VictimPlayer.Id, victimUpdate);

        return scoringResult;
    }

    private async Task<ScrimEventScoringResult> ScoreTeamkillAsync
    (
        ScrimDeathActionEvent death,
        CancellationToken ct
    )
    {
        ScrimEventScoringResult scoringResult = await GetDeathOrDestructionEventPoints
        (
            death.ActionType,
            death.Weapon.ItemCategoryId,
            death.Weapon.Id,
            death.AttackerLoadoutId,
            ct
        );

        int points = scoringResult.Points;
        ScrimEventAggregate attackerUpdate = new()
        {
            Points = points,
            NetScore = points,
            Teamkills = 1
        };

        ScrimEventAggregate victimUpdate = new()
        {
            Deaths = 1,
            TeamkillDeaths = 1
        };

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(death.AttackerCharacterId, attackerUpdate);
        await _teamsManager.UpdatePlayerStats(death.VictimCharacterId, victimUpdate);

        return scoringResult;
    }
    #endregion Death Events

    #region Vehicle Destruction Events
    public async Task<ScrimEventScoringResult> ScoreVehicleDestructionEventAsync
    (
        ScrimVehicleDestructionActionEvent destruction,
        CancellationToken ct = default
    )
    {
        return destruction.DeathType switch
        {
            DeathEventType.Kill => await ScoreVehicleDestruction(destruction, ct),
            DeathEventType.Suicide => await ScoreVehicleTeamDestruction(destruction, ct),
            DeathEventType.Teamkill => await ScoreVehicleSuicideDestruction(destruction, ct),
            _ => new ScrimEventScoringResult(ScrimEventScorePointsSource.Default, 0, false)
        };
    }

    private async Task<ScrimEventScoringResult> ScoreVehicleDestruction
    (
        ScrimVehicleDestructionActionEvent destruction,
        CancellationToken ct
    )
    {
        ScrimEventScoringResult scoringResult = await GetDeathOrDestructionEventPoints
        (
            destruction.ActionType,
            destruction.Weapon.ItemCategoryId,
            destruction.Weapon.Id,
            destruction.AttackerLoadoutId,
            ct
        );

        int points = scoringResult.Points;
        ScrimEventAggregate attackerUpdate = new()
        {
            Points = points,
            NetScore = points,
        };

        ScrimEventAggregate victimUpdate = new()
        {
            NetScore = -points,
        };

        if (destruction.VictimVehicle is not null)
        {
            attackerUpdate.Add(GetVehicleDestroyedEventAggregate(destruction.VictimVehicle.Type));
            victimUpdate.Add(GetVehicleLostEventAggregate(destruction.VictimVehicle.Type));
        }

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(destruction.AttackerCharacterId, attackerUpdate);
        await _teamsManager.UpdatePlayerStats(destruction.VictimCharacterId, victimUpdate);

        return scoringResult;

    }

    private async Task<ScrimEventScoringResult> ScoreVehicleSuicideDestruction
    (
        ScrimVehicleDestructionActionEvent destruction,
        CancellationToken ct
    )
    {
        ScrimEventScoringResult scoringResult = await GetDeathOrDestructionEventPoints
        (
            destruction.ActionType,
            destruction.Weapon.ItemCategoryId,
            destruction.Weapon.Id,
            destruction.AttackerLoadoutId,
            ct
        );

        int points = scoringResult.Points;
        ScrimEventAggregate victimUpdate = new()
        {
            Points = points,
            NetScore = points,
        };

        if (destruction.VictimVehicle is not null)
            victimUpdate.Add(GetVehicleLostEventAggregate(destruction.VictimVehicle.Type));

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(destruction.VictimCharacterId, victimUpdate);

        return scoringResult;
    }

    private async Task<ScrimEventScoringResult> ScoreVehicleTeamDestruction
    (
        ScrimVehicleDestructionActionEvent destruction,
        CancellationToken ct
    )
    {
        ScrimEventScoringResult scoringResult = await GetDeathOrDestructionEventPoints
        (
            destruction.ActionType,
            destruction.Weapon.ItemCategoryId,
            destruction.Weapon.Id,
            destruction.AttackerLoadoutId,
            ct
        );

        int points = scoringResult.Points;
        ScrimEventAggregate attackerUpdate = new()
        {
            Points = points,
            NetScore = points,
        };

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(destruction.AttackerCharacterId, attackerUpdate);

        if (destruction.VictimVehicle is not null)
        {
            ScrimEventAggregate victimUpdate = GetVehicleLostEventAggregate(destruction.VictimVehicle.Type);
            await _teamsManager.UpdatePlayerStats(destruction.VictimCharacterId, victimUpdate);
        }

        return scoringResult;
    }

    private ScrimEventAggregate GetVehicleDestroyedEventAggregate(VehicleType vehicleType)
    {
        return new ScrimEventAggregate()
        {
            VehiclesDestroyed = 1,

            InterceptorsDestroyed = vehicleType == VehicleType.Interceptor ? 1 : 0,
            EsfsDestroyed = vehicleType == VehicleType.ESF ? 1 : 0,
            ValkyriesDestroyed = vehicleType == VehicleType.Valkyrie ? 1 : 0,
            LiberatorsDestroyed = vehicleType == VehicleType.Liberator ? 1 : 0,
            GalaxiesDestroyed = vehicleType == VehicleType.Galaxy ? 1 : 0,
            BastionsDestroyed = vehicleType == VehicleType.Bastion ? 1 : 0,

            FlashesDestroyed = vehicleType == VehicleType.Flash ? 1 : 0,
            HarassersDestroyed = vehicleType == VehicleType.Harasser ? 1 : 0,
            AntsDestroyed = vehicleType == VehicleType.ANT ? 1 : 0,
            SunderersDestroyed = vehicleType == VehicleType.Sunderer ? 1 : 0,
            LightningsDestroyed = vehicleType == VehicleType.Lightning ? 1 : 0,
            MbtsDestroyed = vehicleType == VehicleType.MBT ? 1 : 0
        };
    }

    private ScrimEventAggregate GetVehicleLostEventAggregate(VehicleType vehicleType)
    {
        return new ScrimEventAggregate()
        {
            VehiclesLost = 1,

            InterceptorsLost = vehicleType == VehicleType.Interceptor ? 1 : 0,
            EsfsLost = vehicleType == VehicleType.ESF ? 1 : 0,
            ValkyriesLost = vehicleType == VehicleType.Valkyrie ? 1 : 0,
            LiberatorsLost = vehicleType == VehicleType.Liberator ? 1 : 0,
            GalaxiesLost = vehicleType == VehicleType.Galaxy ? 1 : 0,
            BastionsLost = vehicleType == VehicleType.Bastion ? 1 : 0,

            FlashesLost = vehicleType == VehicleType.Flash ? 1 : 0,
            HarassersLost = vehicleType == VehicleType.Harasser ? 1 : 0,
            AntsLost = vehicleType == VehicleType.ANT ? 1 : 0,
            SunderersLost = vehicleType == VehicleType.Sunderer ? 1 : 0,
            LightningsLost = vehicleType == VehicleType.Lightning ? 1 : 0,
            MbtsLost = vehicleType == VehicleType.MBT ? 1 : 0
        };
    }
    #endregion Vehicle Destruction Events

    #region Experience Events
    public async Task<ScrimEventScoringResult> ScoreReviveEventAsync(ScrimReviveActionEvent revive, CancellationToken ct = default)
    {
        ScrimActionType actionType = revive.ActionType;
        ScrimEventScoringResult scoringResult = GetActionRulePoints(actionType);
        int points = scoringResult.Points;

        ScrimEventAggregate medicUpdate = new()
        {
            Points = points,
            NetScore = points,
            RevivesGiven = 1
        };

        ScrimEventAggregate revivedUpdate = new()
        {
            RevivesTaken = 1
        };

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(revive.MedicCharacterId, medicUpdate);
        await _teamsManager.UpdatePlayerStats(revive.RevivedCharacterId, revivedUpdate);

        return scoringResult;
    }

    public async Task<ScrimEventScoringResult> ScoreAssistEventAsync(ScrimAssistActionEvent assist, CancellationToken ct = default)
    {
        ScrimActionType actionType = assist.ActionType;
        ScrimEventScoringResult scoringResult = GetActionRulePoints(actionType);
        int points = scoringResult.Points;

        ScrimEventAggregate attackerUpdate = new()
        {
            Points = points,
            NetScore = points
        };

        ScrimEventAggregate victimUpdate = new();

        if (actionType == ScrimActionType.DamageAssist)
        {
            attackerUpdate.DamageAssists = 1;
            victimUpdate.DamageAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.DamageTeamAssist)
        {
            attackerUpdate.DamageTeamAssists = 1;
            victimUpdate.DamageAssistedDeaths = 1;
            victimUpdate.DamageTeamAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.DamageSelfAssist)
        {
            victimUpdate.DamageSelfAssists = 1;
            victimUpdate.DamageAssistedDeaths = 1;
            victimUpdate.DamageSelfAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.GrenadeAssist)
        {
            attackerUpdate.GrenadeAssists = 1;
            victimUpdate.GrenadeAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.GrenadeTeamAssist)
        {
            attackerUpdate.GrenadeTeamAssists = 1;
            victimUpdate.GrenadeAssistedDeaths = 1;
            victimUpdate.GrenadeTeamAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.GrenadeSelfAssist)
        {
            victimUpdate.GrenadeSelfAssists = 1;
            victimUpdate.GrenadeAssistedDeaths = 1;
            victimUpdate.GrenadeSelfAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.HealSupportAssist)
        {
            attackerUpdate.HealSupportAssists = 1;
        }
        else if (actionType == ScrimActionType.ProtectAlliesAssist)
        {
            attackerUpdate.ProtectAlliesAssists = 1;
            victimUpdate.ProtectAlliesAssistedDeaths = 1;
        }
        else if (actionType == ScrimActionType.SpotAssist)
        {
            attackerUpdate.SpotAssists = 1;
            victimUpdate.SpotAssistedDeaths = 1;
        }

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(assist.AttackerCharacterId, attackerUpdate);
        if (assist.VictimCharacterId is not null)
            await _teamsManager.UpdatePlayerStats(assist.VictimCharacterId.Value, victimUpdate);

        return scoringResult;
    }

    public async Task<ScrimEventScoringResult> ScoreObjectiveTickEventAsync(ScrimObjectiveTickActionEvent objective, CancellationToken ct = default)
    {
        ScrimActionType actionType = objective.ActionType;
        ScrimEventScoringResult scoringResult = GetActionRulePoints(actionType);
        int points = scoringResult.Points;

        ScrimEventAggregate playerUpdate = new()
        {
            Points = points,
            NetScore = points
        };

        bool isDefense = (actionType == ScrimActionType.PointDefend
            || actionType == ScrimActionType.ObjectiveDefensePulse);

        if (isDefense)
        {
            playerUpdate.ObjectiveDefenseTicks = 1;
        }
        else
        {
            playerUpdate.ObjectiveCaptureTicks = 1;
        }

        // Player Stats update automatically updates the appropriate team's stats
        await _teamsManager.UpdatePlayerStats(objective.PlayerCharacterId, playerUpdate);

        return scoringResult;
    }

    #endregion Experience Events

    #region Objective Events
    public ScrimEventScoringResult ScoreFacilityControlEvent(ScrimFacilityControlActionEvent control)
    {
        TeamDefinition teamOrdinal = control.ControllingTeamOrdinal;
        FacilityControlType type = control.ControlType;

        ScrimActionType actionType = control.ActionType;
        ScrimEventScoringResult scoringResult = GetActionRulePoints(actionType);
        int points = scoringResult.Points;

        ScrimEventAggregate teamUpdate = new()
        {
            Points = points,
            NetScore = points,
            BaseCaptures = (type == FacilityControlType.Capture ? 1 : 0),
            BaseDefenses = (type == FacilityControlType.Defense ? 1 : 0)
        };

        if (actionType == ScrimActionType.FirstBaseCapture)
        {
            teamUpdate.FirstCaptures = 1;
            teamUpdate.FirstCapturePoints = points;
        }
        else if (actionType == ScrimActionType.SubsequentBaseCapture)
        {
            teamUpdate.SubsequentCaptures = 1;
            teamUpdate.SubsequentCapturePoints = points;
        }

        _teamsManager.UpdateTeamStats(teamOrdinal, teamUpdate);

        return scoringResult;
    }
    #endregion Objective Events

    #region Misc. Non-Scored Events

    public void HandlePlayerLogin(IPlayerLogin login)
    {
        ulong characterId = login.CharacterID;
        _teamsManager.SetPlayerOnlineStatus(characterId, true);
    }

    public void HandlePlayerLogout(IPlayerLogout login)
    {
        ulong characterId = login.CharacterID;
        _teamsManager.SetPlayerOnlineStatus(characterId, false);
    }

    #endregion Misc. Non-Scored Events

    #region Rule Handling
    private async Task<ScrimEventScoringResult> GetDeathOrDestructionEventPoints
    (
        ScrimActionType actionType,
        uint? itemCategoryId,
        uint itemId,
        uint attackerLoadoutId,
        CancellationToken ct
    )
    {
        /* Action Rules */
        RulesetActionRule? actionRule = GetActionRule(actionType);

        if (actionRule == null)
        {
            return new ScrimEventScoringResult(ScrimEventScorePointsSource.Default, 0, false);
        }

        if (!actionRule.DeferToItemCategoryRules || itemCategoryId == null)
        {
            return new ScrimEventScoringResult(ScrimEventScorePointsSource.ActionTypeRule, actionRule.Points, false);
        }

        /* Item Category Rules */
        RulesetItemCategoryRule? itemCategoryRule = GetItemCategoryRule(itemCategoryId.Value);
        if (itemCategoryRule == null)
            return new ScrimEventScoringResult(ScrimEventScorePointsSource.ActionTypeRule, actionRule.Points, false);

        if (itemCategoryRule.IsBanned)
        {
            return new ScrimEventScoringResult
            (
                ScrimEventScorePointsSource.ItemCategoryRule,
                itemCategoryRule.Points,
                itemCategoryRule.IsBanned
            );
        }

        if (itemCategoryRule.DeferToPlanetsideClassSettings)
        {
            return await GetPlanetsideClassSettingPoints
            (
                attackerLoadoutId,
                itemCategoryRule.GetPlanetsideClassRuleSettings(),
                ScrimEventScorePointsSource.ItemCategoryRulePlanetsideClassSetting,
                ct
            );
        }

        if (!itemCategoryRule.DeferToItemRules)
        {
            return new ScrimEventScoringResult
            (
                ScrimEventScorePointsSource.ItemCategoryRule,
                itemCategoryRule.Points,
                itemCategoryRule.IsBanned
            );
        }

        /* Item Rules */
        RulesetItemRule? itemRule = GetItemRule(itemId);

        if (itemRule == null)
        {
            return new ScrimEventScoringResult(ScrimEventScorePointsSource.ItemCategoryRule, itemCategoryRule.Points, itemCategoryRule.IsBanned);
        }

        if (itemRule.DeferToPlanetsideClassSettings)
        {
            return await GetPlanetsideClassSettingPoints
            (
                attackerLoadoutId,
                itemRule.GetPlanetsideClassRuleSettings(),
                ScrimEventScorePointsSource.ItemRulePlanetsideClassSetting,
                ct
            );
        }

        return new ScrimEventScoringResult(ScrimEventScorePointsSource.ItemRule, itemRule.Points, itemRule.IsBanned);
    }

    private RulesetActionRule? GetActionRule(ScrimActionType actionType)
        => _activeRuleset?.RulesetActionRules?
            .FirstOrDefault(rule => rule.ScrimActionType == actionType);

    private RulesetItemCategoryRule? GetItemCategoryRule(uint itemCategoryId)
        => _activeRuleset?.RulesetItemCategoryRules?
            .FirstOrDefault(rule => rule.ItemCategoryId == itemCategoryId);

    private RulesetItemRule? GetItemRule(uint itemId)
        => _activeRuleset?.RulesetItemRules?
            .FirstOrDefault(rule => rule.ItemId == itemId);

    private ScrimEventScoringResult GetActionRulePoints(ScrimActionType actionType)
    {
        RulesetActionRule? actionRule = _activeRuleset?.RulesetActionRules?
            .FirstOrDefault(rule => rule.ScrimActionType == actionType);

        return actionRule == null
            ? new ScrimEventScoringResult(ScrimEventScorePointsSource.Default, 0, false)
            : new ScrimEventScoringResult(ScrimEventScorePointsSource.ActionTypeRule, actionRule.Points, false);
    }

    private async Task<ScrimEventScoringResult> GetPlanetsideClassSettingPoints
    (
        uint attackerLoadoutId,
        PlanetsideClassRuleSettings classSettings,
        ScrimEventScorePointsSource scoreSource,
        CancellationToken ct
    )
    {
        CensusProfileType? profileType = await _loadoutService.GetLoadoutProfileTypeAsync(attackerLoadoutId, ct);
        profileType ??= CensusProfileType.HeavyAssault; // Lazy default

        bool isBanned = classSettings.GetClassIsBanned(profileType.Value);
        int points = classSettings.GetClassPoints(profileType.Value);

        return new ScrimEventScoringResult(scoreSource, points, isBanned);
    }

    #endregion Rule Handling
}
