using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Util;

public static class StringHelpers
{
    public static string GetFactionAbbreviation(FactionDefinition? factionId)
        => factionId is null
            ? "No faction"
            : factionId.Value.ToString();
}
