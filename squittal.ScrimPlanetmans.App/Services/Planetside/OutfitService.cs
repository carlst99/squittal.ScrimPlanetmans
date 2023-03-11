using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Models.Planetside;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class OutfitService : IOutfitService
{
    private readonly ICensusOutfitService _outfitService;
    private readonly ICensusCharacterService _characterService;

    public OutfitService
    (
        ICensusOutfitService outfitService,
        ICensusCharacterService characterService
    )
    {
        _outfitService = outfitService;
        _characterService = characterService;
    }

    public async Task<Outfit?> GetByAliasAsync(string alias, CancellationToken ct = default)
    {
        CensusOutfit? outfit = await _outfitService.GetByAliasAsync(alias, ct);
        if (outfit is null)
            return null;

        Outfit convertedEntity = ConvertToDbModel(outfit);
        await ResolveOutfitDetailsAsync(convertedEntity, outfit.LeaderCharacterId);

        return convertedEntity;
    }

    private static Outfit ConvertToDbModel(CensusOutfit censusOutfit)
        => new(censusOutfit.OutfitId, censusOutfit.Name, censusOutfit.Alias)
        {
            MemberCount = censusOutfit.Members.Count
        };

    private async Task ResolveOutfitDetailsAsync(Outfit outfit, ulong leaderCharacterId)
    {
        CensusCharacter? leader = await _characterService.GetByIdAsync(leaderCharacterId);
        if (leader is null || leader.FactionId is FactionDefinition.NSO)
            return;

        outfit.WorldId = (int)leader.WorldId;
        outfit.FactionId = (int)leader.FactionId;
    }
}
