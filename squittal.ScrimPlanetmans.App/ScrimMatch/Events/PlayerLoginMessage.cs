using DbgCensus.EventStream.Abstractions.Objects.Events.Characters;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class PlayerLoginMessage
{
    public Player Player { get; }
    public IPlayerLogin Login { get; }
    public string Info { get; }

    public PlayerLoginMessage(Player player, IPlayerLogin login)
    {
        Player = player;
        Login = login;

        Info = $"Team {Player.TeamOrdinal} player LOGIN: [{Player.OutfitAlias}] {Player.NameDisplay} ({Player.Id})";
    }
}
