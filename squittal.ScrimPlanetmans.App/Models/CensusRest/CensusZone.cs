using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusZone
(
    ZoneDefinition ZoneId,
    GlobalizedString Name
);
