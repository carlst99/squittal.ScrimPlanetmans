using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Util;

public static class StringHelpers
{
    public static string GetFactionAbbreviation(int? factionId)
        => factionId is null
            ? "No faction"
            : ((FactionDefinition)factionId).ToString();
}
