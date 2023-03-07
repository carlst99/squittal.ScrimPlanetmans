using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusServices.Models;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class CharacterService : ICharacterService
{
    private readonly ILogger<CharacterService> _logger;
    private readonly IOutfitService _outfitService;
    private readonly CensusCharacter _censusCharacter;

    public CharacterService
    (
        ILogger<CharacterService> logger,
        IOutfitService outfitService,
        CensusCharacter censusCharacter
    )
    {
        _logger = logger;
        _outfitService = outfitService;
        _censusCharacter = censusCharacter;
    }

    public async Task<Character?> GetCharacterAsync(string characterId)
    {
        try
        {
            CensusCharacterModel? character = await _censusCharacter.GetCharacter(characterId);
            return character is null
                ? null
                : ConvertToDbModel(character);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve character by ID from Census");
            return null;
        }
    }

    public async Task<Character?> GetCharacterByNameAsync(string characterName)
    {
        try
        {
            CensusCharacterModel? character = await _censusCharacter.GetCharacterByName(characterName);
            return character is null
                ? null
                : ConvertToDbModel(character);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve character by name from Census");
            return null;
        }
    }

    public async Task<OutfitMember?> GetCharacterOutfitAsync(string characterId)
    {
        try
        {
            Character? character = await GetCharacterAsync(characterId);
            if (character is null)
                return null;

            return await _outfitService.UpdateCharacterOutfitMembershipAsync(character);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve a character's outfit from Census");
            return null;
        }
    }

    public static Character ConvertToDbModel(CensusCharacterModel censusModel)
    {
        bool isOnline = int.TryParse(censusModel.OnlineStatus, out int onlineStatus)
            && onlineStatus > 0;

        return new Character
        {
            Id = censusModel.CharacterId,
            Name = censusModel.Name.First,
            FactionId = censusModel.FactionId,
            TitleId = censusModel.TitleId,
            WorldId = censusModel.WorldId,
            BattleRank = censusModel.BattleRank.Value,
            BattleRankPercentToNext = censusModel.BattleRank.PercentToNext,
            CertsEarned = censusModel.Certs.EarnedPoints,
            PrestigeLevel = censusModel.PrestigeLevel,
            IsOnline = isOnline
        };
    }
}
