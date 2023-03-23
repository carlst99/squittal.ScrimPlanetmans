using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusVehicle
(
    uint VehicleId,
    GlobalizedString Name
);
