using DbgCensus.Core.Objects;

namespace squittal.ScrimPlanetmans.App.Models.CensusRest;

public record CensusCharacter
(
    ulong CharacterId,
    CensusCharacter.CharacterName Name,
    FactionDefinition FactionId,
    int PrestigeLevel,
    WorldDefinition WorldId,
    CensusCharacter.CharacterOutfit? Outfit
)
{
    public record CharacterName(string First);

    public record CharacterOutfit(ulong OutfitId, string Alias);
}
