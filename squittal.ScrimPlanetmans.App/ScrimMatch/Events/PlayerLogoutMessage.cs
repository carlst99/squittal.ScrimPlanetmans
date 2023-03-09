using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class PlayerLogoutMessage
{
    public Player Player { get; set; }
    public IPlayerLogout Logout { get; set; }
    public string Info { get; set; }

    public PlayerLogoutMessage(Player player, IPlayerLogout logout)
    {
        Player = player;
        Logout = logout;

        Info = $"Team {Player.TeamOrdinal} player LOGOUT: [{Player.OutfitAlias}] {Player.NameDisplay} ({Player.Id})";
    }
}
