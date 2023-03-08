using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Rest.Abstractions;
using DbgCensus.Rest.Abstractions.Queries;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Services.CensusRest;

public class CensusCharacterService : BaseCensusService, ICensusCharacterService
{
    private readonly IMemoryCache _cache;

    public CensusCharacterService
    (
        ILogger<CensusCharacterService> logger,
        IQueryService queryService,
        IMemoryCache cache
    ) : base(logger, queryService)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<CensusCharacter?> GetByIdAsync(ulong characterId, CancellationToken ct = default)
    {
        object cacheKey = (typeof(CensusCharacter), characterId);

        if (_cache.TryGetValue(cacheKey, out CensusCharacter? character))
            return character;

        IQueryBuilder query = CreateDefaultCharacterQuery()
            .Where("character_id", SearchModifier.Equals, characterId);

        character = await GetAsync<CensusCharacter>(query, ct);

        if (character is not null)
            _cache.Set(cacheKey, character, GetCharacterCacheOptions());

        return character;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CensusCharacter>?> GetByIdAsync
    (
        IEnumerable<ulong> characterIds,
        CancellationToken ct = default
    )
    {
        List<CensusCharacter> retrieved = new();
        List<ulong> toRetrieve = new();

        foreach (ulong id in characterIds)
        {
            object cacheKey = GetCharacterCacheKey(id);
            if (_cache.TryGetValue(cacheKey, out CensusCharacter? character) && character is not null)
                retrieved.Add(character);
            else
                toRetrieve.Add(id);
        }

        if (toRetrieve.Count is 0)
            return retrieved;

        IQueryBuilder query = CreateDefaultCharacterQuery()
            .Where("character_id", SearchModifier.Equals, toRetrieve);

        IReadOnlyList<CensusCharacter>? characters = await GetListAsync<CensusCharacter>(query, ct);
        if (characters is not null)
        {
            foreach (CensusCharacter character in characters)
            {
                object cacheKey = GetCharacterCacheKey(character.CharacterId);
                _cache.Set(cacheKey, character, GetCharacterCacheOptions());
            }
        }

        return retrieved;
    }

    /// <inheritdoc />
    public async Task<CensusCharacter?> GetByNameAsync(string characterName, CancellationToken ct = default)
    {
        IQueryBuilder query = CreateDefaultCharacterQuery()
            .Where("name.first_lower", SearchModifier.Equals, characterName.ToLower());

        return await GetAsync<CensusCharacter>(query, ct);
    }

    /// <inheritdoc />
    public async Task<bool?> GetOnlineStatusAsync(ulong characterId, CancellationToken ct = default)
    {
        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("characters_online_status")
            .Where("character_id", SearchModifier.Equals, characterId);

        CensusCharactersOnlineStatus? status = await GetAsync<CensusCharactersOnlineStatus>(query, ct);

        return status?.OnlineStatus;
    }

    public async Task<IReadOnlyList<CensusCharactersOnlineStatus>?> GetOnlineStatusAsync
    (
        IEnumerable<ulong> characterIds,
        CancellationToken ct = default
    )
    {
        IQueryBuilder query = QueryService.CreateQuery()
            .OnCollection("characters_online_status")
            .Where("character_id", SearchModifier.Equals, characterIds);

        return await GetListAsync<CensusCharactersOnlineStatus>(query, ct);
    }

    private static object GetCharacterCacheKey(ulong characterId)
        => (typeof(CensusCharacter), characterId);

    private static MemoryCacheEntryOptions GetCharacterCacheOptions()
        => new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            Priority = CacheItemPriority.Low
        };

    private IQueryBuilder CreateDefaultCharacterQuery()
        => QueryService.CreateQuery()
            .OnCollection("character")
            .ShowFields
            (
                "character_id",
                "name.first",
                "faction_id",
                "prestige_level"
            )
            .AddResolve("world")
            .AddResolve("outfit", "outfit_id", "alias");
}
