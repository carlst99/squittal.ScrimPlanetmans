using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Util;

public static class SqCssHelper
{
    public static string GetFactionClassFromId(int? factionId)
        => factionId switch
        {
            1 => "vs",
            2 => "nc",
            3 => "tr",
            4 => "ns",
            _ => "default",
        };

    public static string GetFactionClassFromId(FactionDefinition? factionId)
        => factionId switch
        {
            FactionDefinition.VS => "vs",
            FactionDefinition.NC => "nc",
            FactionDefinition.TR => "tr",
            FactionDefinition.NSO => "ns",
            _ => "default",
        };

    public static string GetOnlineStatusEmoji(bool isOnline)
        => isOnline
            ? "🌐"
            : "💤";

    public static string GetParticipatingStatusEmoji(bool isParticipating)
        => isParticipating ? "∙" : string.Empty;

    public static string GetZoneDisplayEmojiFromName(string zoneName)
        => zoneName switch
        {
            "Amerish" => "🗻",
            "Esamir" => "❄️",
            "Hossin" => "🌳",
            "Indar" => "☀️",
            "Oshur" => "🌊",
            _ => "❔",
        };

    public static string GetFactionAbbreviation(FactionDefinition? factionId)
        => factionId is null
            ? "None"
            : factionId.Value.ToString();
}
