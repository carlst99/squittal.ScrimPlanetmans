﻿using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.Planetside;

public class Outfit
{
    public ulong Id { get; }
    public string Name { get; }
    public string Alias { get; }
    public string AliasLower { get; }

    public int MemberCount { get; set; }
    public int MembersOnlineCount { get; set; }
    public FactionDefinition? FactionId { get; set; }
    public WorldDefinition? WorldId { get; set; }
    public TeamDefinition? TeamOrdinal { get; set; }

    public Outfit(ulong id, string name, string alias)
    {
        Id = id;
        Name = name;
        Alias = alias;
        AliasLower = alias.ToLower();
    }
}
