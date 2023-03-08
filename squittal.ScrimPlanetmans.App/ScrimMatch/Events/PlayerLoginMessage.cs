using squittal.ScrimPlanetmans.App.Models.Planetside.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class PlayerLoginMessage
{
    public Player Player { get; }
    public PlayerLogin Login { get; }
    public string Info { get; }

    public PlayerLoginMessage(Player player, PlayerLogin login)
    {
        Player = player;
        Login = login;

        Info = $"Team {Player.TeamOrdinal} player LOGIN: [{Player.OutfitAlias}] {Player.NameDisplay} ({Player.Id})";
    }
}
