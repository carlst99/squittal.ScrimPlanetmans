﻿using System;
using System.ComponentModel.DataAnnotations;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Data.Models;

public class ScrimVehicleDestruction
{
    [Required]
    public string ScrimMatchId { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public ulong AttackerCharacterId { get; set; }

    [Required]
    public ulong VictimCharacterId { get; set; }
    [Required]
    public int VictimVehicleId { get; set; }

    public int? AttackerVehicleId { get; set; }

    public int ScrimMatchRound { get; set; }

    public ScrimActionType ActionType { get; set; }
    public DeathEventType DeathType { get; set; }

    public TeamDefinition AttackerTeamOrdinal { get; set; }
    public TeamDefinition VictimTeamOrdinal { get; set; }

    public VehicleType? AttackerVehicleClass { get; set; }
    public VehicleType? VictimVehicleClass { get; set; }

    public string AttackerNameFull { get; set; }
    public int AttackerFactionId { get; set; }
    public int? AttackerLoadoutId { get; set; }
    public ulong? AttackerOutfitId { get; set; }
    public string? AttackerOutfitAlias { get; set; }
    public bool AttackerIsOutfitless { get; set; }

    public string VictimNameFull { get; set; }
    public int VictimFactionId { get; set; }
    public int? VictimLoadoutId { get; set; }
    public ulong? VictimOutfitId { get; set; }
    public string? VictimOutfitAlias { get; set; }
    public bool VictimIsOutfitless { get; set; }

    public int? WeaponId { get; set; }
    public int? WeaponItemCategoryId { get; set; }
    public bool? IsVehicleWeapon { get; set; }

    public int WorldId { get; set; }
    public int ZoneId { get; set; }

    public int Points { get; set; }
    public int? AttackerResultingPoints { get; set; }
    public int? AttackerResultingNetScore { get; set; }
    public int? VictimResultingPoints { get; set; }
    public int? VictimResultingNetScore { get; set; }

    #region Navigation Properties
    public ScrimMatch ScrimMatch { get; set; }
    public Faction AttackerFaction { get; set; }
    public Faction VictimFaction { get; set; }
    public Vehicle AttackerVehicle { get; set; }
    public Vehicle VictimVehicle { get; set; }
    public Item Weapon { get; set; }
    public ItemCategory WeaponItemCategory { get; set; }
    public World World { get; set; }
    public Zone Zone { get; set; }
    #endregion Navigation Properties
}
