using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public record TeamFactionChangeMessage
(
    TeamDefinition Ordinal,
    FactionDefinition? NewFactionId,
    string NewFactionAbbreviation,
    FactionDefinition? OldFactionId,
    string OldFactionAbbreviation
);
