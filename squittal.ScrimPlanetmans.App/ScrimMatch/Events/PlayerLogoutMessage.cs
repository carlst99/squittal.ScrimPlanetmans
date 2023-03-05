using squittal.ScrimPlanetmans.App.Models.Planetside.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class PlayerLogoutMessage
{
    public Player Player { get; set; }
    public PlayerLogout Logout { get; set; }
    public string Info { get; set; } = string.Empty;

    public PlayerLogoutMessage(Player player, PlayerLogout logout)
    {
        Player = player;
        Logout = logout;

        Info = $"Team {Player.TeamOrdinal} player LOGOUT: [{Player.OutfitAlias}] {Player.NameDisplay} ({Player.Id})";
    }
}
