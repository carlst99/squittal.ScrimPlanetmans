using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch;

public class ScrimPlayersService : IScrimPlayersService
{
    private readonly ICensusOutfitService _outfitService;
    private readonly ICensusCharacterService _characterService;

    public ScrimPlayersService(ICensusOutfitService outfitService, ICensusCharacterService characterService)
    {
        _outfitService = outfitService;
        _characterService = characterService;
    }

    public async Task<Player?> GetByIdAsync(ulong characterId, CancellationToken ct = default)
    {
        CensusCharacter? character = await _characterService.GetByIdAsync(characterId, ct);
        bool? isOnline = await _characterService.GetOnlineStatusAsync(characterId, ct);

        return character is null
            ? null
            : new Player(character, isOnline ?? false);
    }

    public async Task<IEnumerable<Player>?> GetByIdAsync
    (
        IEnumerable<ulong> characterIds,
        CancellationToken ct = default
    )
    {
        // ReSharper disable PossibleMultipleEnumeration
        IReadOnlyList<CensusCharacter>? characters
            = await _characterService.GetByIdAsync(characterIds, ct);
        IReadOnlyList<CensusCharactersOnlineStatus>? onlineStatus
            = await _characterService.GetOnlineStatusAsync(characterIds, ct);
        // ReSharper restore PossibleMultipleEnumeration

        if (characters is null)
            return null;

        Player[] players = new Player[characters.Count];
        for (int i = 0; i < characters.Count; i++)
        {
            CensusCharacter character = characters[i];
            bool isOnline = onlineStatus?.FirstOrDefault(o => o.CharacterId == character.CharacterId)?
                .OnlineStatus ?? false;

            players[i] = new Player(character, isOnline);
        }

        return players;
    }

    public async Task<Player?> GetByNameAsync(string characterName, CancellationToken ct = default)
    {
        CensusCharacter? character = await _characterService.GetByNameAsync(characterName, ct);
        if (character is null)
            return null;

        bool? isOnline = await _characterService.GetOnlineStatusAsync(character.CharacterId, ct);

        return new Player(character, isOnline ?? false);
    }

    public async Task<IEnumerable<Player>?> GetPlayersFromOutfitAliasAsync(string alias, CancellationToken ct = default)
    {
        CensusOutfit? outfit = await _outfitService.GetByAliasAsync(alias, ct);
        if (outfit is null)
            return null;

        IReadOnlyList<CensusCharacter>? characters = await _characterService.GetByIdAsync
        (
            outfit.Members.Select(m => m.CharacterId),
            ct
        );

        if (characters is null)
            return null;

        IReadOnlyList<CensusCharactersOnlineStatus>? onlineStatus = await _characterService.GetOnlineStatusAsync
        (
            outfit.Members.Select(m => m.CharacterId),
            ct
        );

        return characters.Select
        (
            c => new Player
            (
                c,
                onlineStatus?.FirstOrDefault(x => x.CharacterId == c.CharacterId)?.OnlineStatus ?? false
            )
        );
    }
}
