using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public record TeamAliasChangeMessage(TeamDefinition Ordinal, string NewAlias, string OldAlias);
