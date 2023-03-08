using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaybreakGames.Census.Exceptions;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusServices.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Models.Planetside;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.Planetside;

public class OutfitService : IOutfitService
{
    private readonly CensusOutfit _censusOutfit;
    private readonly ICensusCharacterService _characterService;
    private readonly ILogger<OutfitService> _logger;

    public OutfitService(CensusOutfit censusOutfit, ICensusCharacterService characterService, ILogger<OutfitService> logger)
    {
        _censusOutfit = censusOutfit;
        _characterService = characterService;
        _logger = logger;
    }

    public async Task<Outfit?> GetOutfitAsync(string outfitId)
        => await GetOutfitInternalAsync(outfitId);

    public async Task<Outfit?> GetOutfitByAliasAsync(string alias)
    {
        CensusOutfitModel? outfit = await _censusOutfit.GetOutfitByAliasAsync(alias);
        if (outfit is null)
            return null;

        Outfit censusEntity = ConvertToDbModel(outfit);

        if (censusEntity.MemberCount == 0)
            return censusEntity;

        return await ResolveOutfitDetailsAsync(censusEntity, null);
    }

    public async Task<IEnumerable<CensusCharacter>?> GetOutfitMembersByAliasAsync(string alias)
    {
        IEnumerable<CensusOutfitMemberCharacterModel>? members = await _censusOutfit.GetOutfitMembersByAliasAsync(alias);

        return members?.Where(m => m is { CharacterId: { }, Name: { } })
            .Select(ConvertToDbModel);
    }

    private async Task<Outfit?> GetOutfitInternalAsync(string outfitId, Character? member = null)
    {
        Outfit? outfit = await GetKnownOutfitAsync(outfitId);
        if (outfit is null)
            return null;

        // These are null if outfit was retrieved from the census API
        if (outfit.WorldId == null || outfit.FactionId == null)
            outfit = await ResolveOutfitDetailsAsync(outfit, member);

        return outfit;
    }

    // Returns the specified outfit from the database, if it exists. Otherwise,
    // queries for it in the DBG census.
    private async Task<Outfit?> GetKnownOutfitAsync(string outfitId)
    {
        try
        {
            return await GetCensusOutfitAsync(outfitId);
        }
        catch (CensusConnectionException)
        {
            return null;
        }
    }

    private async Task<Outfit?> GetCensusOutfitAsync(string outfitId)
    {
        try
        {
            CensusOutfitModel? censusOutfit = await _censusOutfit.GetOutfitAsync(outfitId);
            return censusOutfit is null
                ? null
                : ConvertToDbModel(censusOutfit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get outfit by ID from Census");
            return null;
        }
    }

    public static Outfit ConvertToDbModel(CensusOutfitModel censusOutfit)
    {
        return new Outfit
        {
            Id = censusOutfit.OutfitId,
            Alias = censusOutfit.Alias,
            AliasLower = censusOutfit.AliasLower,
            Name = censusOutfit.Name,
            LeaderCharacterId = censusOutfit.LeaderCharacterId,
            CreatedDate = censusOutfit.TimeCreated,
            MemberCount = censusOutfit.MemberCount
        };
    }

    private async Task<Outfit> ResolveOutfitDetailsAsync(Outfit outfit, Character? member)
    {
        if (member != null)
        {
            outfit.WorldId = member.WorldId;
            outfit.FactionId = member.FactionId;
        }
        else
        {
            CensusCharacter? leader = await _characterService.GetByIdAsync(outfit.LeaderCharacterId);
            outfit.WorldId = leader?.WorldId;
            outfit.FactionId = leader?.FactionId;
        }

        return outfit;
    }

    public static CensusCharacter ConvertToDbModel(CensusOutfitMemberCharacterModel member)
    {
        return new CensusCharacter
        (
            member.CharacterId,
            member.Name,
            member.FactionId,
            PrestigeLevel = member.PrestigeLevel,
            member.WorldId,
            new CensusCharacter.CharacterOutfit(member.OutfitId, member.OutfitAlias)
        );
    }
}
