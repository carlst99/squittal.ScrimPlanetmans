using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusItem
(
    uint ItemId,
    uint ItemCategoryId,
    bool IsVehicleWeapon,
    GlobalizedString? Name,
    FactionDefinition FactionId
);
