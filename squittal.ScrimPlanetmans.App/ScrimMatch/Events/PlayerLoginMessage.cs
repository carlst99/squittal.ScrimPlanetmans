using squittal.ScrimPlanetmans.App.Models.Planetside.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class PlayerLoginMessage
{
    public Player Player { get; set; }
    public PlayerLogin Login { get; set; }
    public string Info { get; set; } = string.Empty;

    public PlayerLoginMessage(Player player, PlayerLogin login)
    {
        Player = player;
        Login = login;

        Info = $"Team {Player.TeamOrdinal} player LOGIN: [{Player.OutfitAlias}] {Player.NameDisplay} ({Player.Id})";
    }
}
