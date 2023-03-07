using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class TeamPlayerChangeMessage
{
    public Player Player { get; set; }

    public string PlayerId { get; set; }
    public string PlayerNameDisplay { get; set; }
    public bool IsOnline { get; set; }
    public TeamDefinition TeamOrdinal { get; set; }

    public string OutfitId { get; set; }
    public string OutfitAlias { get; set; }
    public string OutfitAliasLower { get; set; }

    public bool IsOutfitless { get; set; } // assume most players will be added via outfits
    public bool IsLastOfOutfit { get; set; }

    public TeamPlayerChangeType ChangeType { get; private set; }

    public string Info => GetInfoMessage();

    public TeamPlayerChangeMessage(Player player, TeamPlayerChangeType type, bool isLastOfOutfit = false)
    {
        Player = player;
        ChangeType = type;

        PlayerId = player.Id;
        PlayerNameDisplay = player.NameDisplay;
        TeamOrdinal = player.TeamOrdinal;
        IsOnline = player.IsOnline;

        OutfitId = player.OutfitId;
        OutfitAlias = player.OutfitAlias;
        OutfitAliasLower = player.OutfitAliasLower;

        IsOutfitless = !string.IsNullOrWhiteSpace(OutfitAliasLower);
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
    SubstitueIn = 3,
    SubstitueOut = 4,
    SetActive = 5,
    SetInactive = 6
};
