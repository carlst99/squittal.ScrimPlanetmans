namespace squittal.ScrimPlanetmans.App.Models.Planetside;

public class Outfit
{
    public ulong Id { get; }
    public string Name { get; }
    public string Alias { get; }
    public string AliasLower { get; }

    public int MemberCount { get; set; }
    public int MembersOnlineCount { get; set; }
    public int? FactionId { get; set; }
    public int? WorldId { get; set; }
    public TeamDefinition? TeamOrdinal { get; set; }

    public Outfit(ulong id, string name, string alias)
    {
        Id = id;
        Name = name;
        Alias = alias;
        AliasLower = alias.ToLower();
    }
}
