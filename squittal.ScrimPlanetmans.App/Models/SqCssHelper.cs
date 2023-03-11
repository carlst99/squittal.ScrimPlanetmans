namespace squittal.ScrimPlanetmans.App.Models;

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
}
