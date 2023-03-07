using squittal.ScrimPlanetmans.App.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public record TeamLockStatusChangeMessage(TeamDefinition TeamOrdinal, bool IsLocked);
