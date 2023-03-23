using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusWorld
(
    WorldDefinition WorldId,
    GlobalizedString Name
);
