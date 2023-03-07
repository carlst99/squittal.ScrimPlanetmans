using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public record TeamFactionChangeMessage
(
    TeamDefinition Ordinal,
    int? NewFactionId,
    string NewFactionAbbreviation,
    int? OldFactionId,
    string OldFactionAbbreviation
);
