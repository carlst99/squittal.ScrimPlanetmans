using Microsoft.SqlServer.Management.Sdk.Sfc;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusOutfit
(
    ulong OutfitId,
    string Name,
    string Alias,
    ulong LeaderCharacterId,
    IReadOnlyList<CensusOutfit.OutfitMember> Members
)
{
    public readonly record struct OutfitMember(ulong CharacterId);
}
