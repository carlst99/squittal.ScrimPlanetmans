using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class TeamPlayerChangeMessage
{
    public Player Player { get; set; }

    public ulong PlayerId { get; set; }
    public bool IsLastOfOutfit { get; set; }

    public TeamPlayerChangeType ChangeType { get; private set; }

    public string Info => GetInfoMessage();

    public TeamPlayerChangeMessage(Player player, TeamPlayerChangeType type, bool isLastOfOutfit = false)
    {
        Player = player;
        ChangeType = type;
        PlayerId = player.Id;
        IsLastOfOutfit = isLastOfOutfit;
    }

    private string GetInfoMessage()
    {
        string type = ChangeType != TeamPlayerChangeType.Default
            ? ChangeType.ToString().ToUpper()
            : string.Empty;

        string online = Player.IsOnline
            ? " ONLINE"
            : string.Empty;

        return $"Team {Player.TeamOrdinal} Player {type}: {Player.NameDisplay} [{Player.Id}]{online}";
    }
}

// TODO: replace all instances of this with TeamChangeType
public enum TeamPlayerChangeType
{
    Default = 0,
    Add = 1,
    Remove = 2,
    SubstituteIn = 3,
    SubstituteOut = 4,
    SetActive = 5,
    SetInactive = 6
};
