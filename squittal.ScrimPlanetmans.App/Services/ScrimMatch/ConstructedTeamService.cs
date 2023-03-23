using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DbgCensus.Core.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Models.Forms;
using squittal.ScrimPlanetmans.App.ScrimMatch.Events;
using squittal.ScrimPlanetmans.App.ScrimMatch.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch;

public class ConstructedTeamService : IConstructedTeamService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly ICensusCharacterService _characterService;
    private readonly IScrimPlayersService _playerService;
    private readonly IScrimMessageBroadcastService _messageService;
    private readonly ILogger<ConstructedTeamService> _logger;

    private readonly ConcurrentDictionary<int, ConstructedTeam> _constructedTeamsMap = new();
    private readonly KeyedSemaphoreSlim _constructedTeamLock = new();

    public static Regex ConstructedTeamNameRegex { get; } = new("^([A-Za-z0-9()\\[\\]\\-_][ ]{0,1}){1,49}[A-Za-z0-9()\\[\\]\\-_]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static Regex ConstructedTeamAliasRegex { get; } = new("^[A-Za-z0-9]{1,4}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    public static Regex CharacterNameRegex { get; } = new("^[A-Za-z0-9]{1,32}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);


    public ConstructedTeamService
    (
        IDbContextHelper dbContextHelper,
        ICensusCharacterService characterService,
        IScrimPlayersService playerService,
        IScrimMessageBroadcastService messageService,
        ILogger<ConstructedTeamService> logger
    )
    {
        _dbContextHelper = dbContextHelper;
        _characterService = characterService;
        _playerService = playerService;
        _messageService = messageService;
        _logger = logger;
    }


    #region GET Methods

    public async Task<ConstructedTeam?> GetConstructedTeamAsync
    (
        int teamId,
        bool ignoreCollections = false,
        CancellationToken ct = default
    )
    {
        if (_constructedTeamsMap.IsEmpty)
            await SetUpConstructedTeamsMapAsync(ct);

        _constructedTeamsMap.TryGetValue(teamId, out ConstructedTeam? team);
        if (ignoreCollections || team == null)
            return team;

        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            team.PlayerMemberships = await dbContext.ConstructedTeamPlayerMemberships
                .Where(m => m.ConstructedTeamId == teamId)
                .ToListAsync(cancellationToken: ct);

            return team;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get constructed team");
            return null;
        }
    }

    public async Task<int> GetConstructedTeamMemberCountAsync(int teamId, CancellationToken ct = default)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ConstructedTeamPlayerMemberships
                .Where(m => m.ConstructedTeamId == teamId)
                .CountAsync(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get constructed team member count");
            return -1;
        }
    }

    public async Task<IEnumerable<ulong>> GetConstructedTeamFactionMemberIdsAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ConstructedTeamPlayerMemberships
            .Where(m => m.ConstructedTeamId == teamId && m.FactionId == factionId)
            .Select(m => m.CharacterId)
            .ToListAsync(ct);
    }

    private async Task<List<ConstructedTeamPlayerMembership>> GetConstructedTeamFactionMembersAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ConstructedTeamPlayerMemberships
            .Where(m => m.ConstructedTeamId == teamId && m.FactionId == factionId)
            .ToListAsync(cancellationToken: ct);
    }

    public async Task<IEnumerable<CensusCharacter>> GetConstructedTeamFactionCharactersAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    )
    {
        List<ConstructedTeamPlayerMembership> unprocessedMembers = await GetConstructedTeamFactionMembersAsync
        (
            teamId,
            factionId,
            ct
        );

        if (unprocessedMembers.Count is 0)
            return Array.Empty<CensusCharacter>();

        IReadOnlyList<CensusCharacter>? retrievedCharacters = await _characterService.GetByIdAsync
        (
            unprocessedMembers.Select(m => m.CharacterId),
            ct
        );

        List<CensusCharacter> processedCharacters = new();

        if (retrievedCharacters is not null)
        {
            foreach (CensusCharacter character in retrievedCharacters)
            {
                unprocessedMembers.RemoveAll(m => m.CharacterId == character.CharacterId);
                processedCharacters.Add(character);
            }
        }

        foreach (ConstructedTeamPlayerMembership member in unprocessedMembers)
        {
            CensusCharacter character = new
            (
                member.CharacterId,
                new CensusCharacter.CharacterName($"Unknown{member.CharacterId}"),
                (FactionDefinition)member.FactionId,
                0,
                WorldDefinition.Jaeger,
                null
            );

            processedCharacters.Add(character);
        }

        return processedCharacters;
    }

    public async Task<IEnumerable<ConstructedTeamMemberDetails>> GetConstructedTeamFactionMemberDetailsAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    )
    {
        List<ConstructedTeamPlayerMembership> unprocessedMembers = await GetConstructedTeamFactionMembersAsync
        (
            teamId,
            factionId,
            ct
        );

        if (unprocessedMembers.Count is 0)
            return Array.Empty<ConstructedTeamMemberDetails>();

        List<ConstructedTeamMemberDetails> processedCharacters = new();
        IReadOnlyList<CensusCharacter>? retrievedCharacters = await _characterService.GetByIdAsync
        (
            unprocessedMembers.Select(m => m.CharacterId),
            ct
        );

        if (retrievedCharacters is not null)
        {
            foreach (CensusCharacter character in retrievedCharacters)
            {
                ConstructedTeamPlayerMembership member = unprocessedMembers.First
                (
                    m => m.CharacterId == character.CharacterId
                );
                processedCharacters.Add(ConvertToMemberDetailsModel(character, member));

                unprocessedMembers.RemoveAll(m => m.CharacterId == character.CharacterId);
            }
        }

        foreach (ConstructedTeamPlayerMembership member in unprocessedMembers)
        {
            processedCharacters.Add(new ConstructedTeamMemberDetails
            {
                ConstructedTeamId = member.ConstructedTeamId,
                NameFull = $"uc{member.CharacterId}",
                NameAlias = null,
                CharacterId = member.CharacterId,
                FactionId = member.FactionId
            });
        }

        foreach (ConstructedTeamMemberDetails member in processedCharacters)
            await SetConstructedTeamMemberIsMatchParticipantAsync(member, ct);

        return processedCharacters;
    }

    private static ConstructedTeamMemberDetails ConvertToMemberDetailsModel
    (
        CensusCharacter character,
        ConstructedTeamPlayerMembership membership
    )
    {
        return new ConstructedTeamMemberDetails
        {
            CharacterId = membership.CharacterId,
            ConstructedTeamId = membership.ConstructedTeamId,
            FactionId = membership.FactionId,
            NameFull = character.Name.First,
            NameAlias = membership.Alias,
            PrestigeLevel = character.PrestigeLevel,
            WorldId = character.WorldId
        };
    }

    public async Task<IEnumerable<Player>> GetConstructedTeamFactionPlayersAsync
    (
        int teamId,
        int factionId,
        CancellationToken ct = default
    )
    {
        List<ConstructedTeamPlayerMembership> unprocessedMembers = await GetConstructedTeamFactionMembersAsync
        (
            teamId,
            factionId,
            ct
        );

        if (unprocessedMembers.Count is 0)
            return Array.Empty<Player>();

        List<Player> processedPlayers = new();
        IEnumerable<Player>? retrievedPlayers = await _playerService.GetByIdAsync
        (
            unprocessedMembers.Select(m => m.CharacterId),
            ct
        );

        if (retrievedPlayers is not null)
        {
            foreach (Player player in retrievedPlayers)
            {
                string? playerAlias = unprocessedMembers.FirstOrDefault(m => m.CharacterId == player.Id)?.Alias;

                player.TrySetNameAlias(playerAlias);

                processedPlayers.Add(player);
                unprocessedMembers.RemoveAll(m => m.CharacterId == player.Id);
            }
        }

        foreach (ConstructedTeamPlayerMembership member in unprocessedMembers)
        {
            string name = string.IsNullOrWhiteSpace(member.Alias)
                ? $"Unknown{member.CharacterId}"
                : member.Alias;

            CensusCharacter character = new
            (
                member.CharacterId,
                new CensusCharacter.CharacterName($"Unknown{member.CharacterId}"),
                (FactionDefinition)member.FactionId,
                0,
                WorldDefinition.Jaeger,
                null
            );

            Player player = new(character, false);
            player.TrySetNameAlias(name);

            processedPlayers.Add(player);
        }

        return processedPlayers;
    }

    public async Task<ConstructedTeamFormInfo?> GetConstructedTeamFormInfoAsync
    (
        int teamId,
        bool ignoreCollections = false,
        CancellationToken ct = default
    )
    {
        ConstructedTeam? constructedTeam = await GetConstructedTeamAsync(teamId, ignoreCollections, ct);
        if (constructedTeam is null)
            return null;

        ConstructedTeamFormInfo teamInfo = ConvertToTeamFormInfo(constructedTeam);

        if (ignoreCollections || !constructedTeam.PlayerMemberships.Any())
            return teamInfo;

        IReadOnlyList<CensusCharacter>? teamCharacters = await _characterService.GetByIdAsync
        (
            constructedTeam.PlayerMemberships.Select(m => m.CharacterId),
            ct
        );

        teamInfo.Characters = teamCharacters;

        return teamInfo;
    }

    private static ConstructedTeamFormInfo ConvertToTeamFormInfo(ConstructedTeam constructedTeam)
    {
        return new ConstructedTeamFormInfo
        {
            Id = constructedTeam.Id,
            Name = constructedTeam.Name,
            Alias = constructedTeam.Alias,
            IsHiddenFromSelection = constructedTeam.IsHiddenFromSelection
        };
    }

    public async Task<IEnumerable<ConstructedTeam>> GetConstructedTeamsAsync
    (
        bool includeHiddenTeams = false,
        CancellationToken ct = default
    )
    {
        if (_constructedTeamsMap.IsEmpty)
            await SetUpConstructedTeamsMapAsync(ct);

        return includeHiddenTeams
            ? _constructedTeamsMap.Values.ToList()
            : _constructedTeamsMap.Values.Where(t => !t.IsHiddenFromSelection).ToList();
    }

    #endregion GET Methods

    #region CREATE / EDIT Methods

    public async Task<bool> UpdateConstructedTeamInfoAsync
    (
        ConstructedTeam teamUpdate,
        CancellationToken ct = default
    )
    {
        int updateId = teamUpdate.Id;
        string updateName = teamUpdate.Name;
        string updateAlias = teamUpdate.Alias;
        bool updateIsHidden = teamUpdate.IsHiddenFromSelection;

        if (!IsValidConstructedTeamName(updateName))
        {
            _logger.LogError("Error updating a constructed team's {Id} info: invalid team name", updateId);
            return false;
        }

        if (!IsValidConstructedTeamAlias(updateAlias))
        {
            _logger.LogError("Error updating a constructed team's {ID} info: invalid team alias", updateId);
            return false;
        }

        using (await _constructedTeamLock.WaitAsync($"{updateId}", ct))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                ConstructedTeam? storeEntity = await GetConstructedTeamAsync(updateId, true, ct);

                if (storeEntity == null)
                {
                    return false;
                }

                string oldName = storeEntity.Name;
                string oldAlias = storeEntity.Alias;
                bool oldIsHidden = storeEntity.IsHiddenFromSelection;

                storeEntity.Name = updateName;
                storeEntity.Alias = updateAlias;
                storeEntity.IsHiddenFromSelection = updateIsHidden;

                dbContext.ConstructedTeams.Update(storeEntity);

                await dbContext.SaveChangesAsync(ct);

                await SetUpConstructedTeamsMapAsync(ct);

                ConstructedTeamInfoChangeMessage message = new(storeEntity, oldName, oldAlias, oldIsHidden);
                _messageService.BroadcastConstructedTeamInfoChangeMessage(message);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error update Constructed Team {ID} info", updateId);
                return false;
            }
        }
    }

    public async Task<ConstructedTeam?> CreateConstructedTeamAsync
    (
        ConstructedTeam constructedTeam,
        CancellationToken ct = default
    )
    {
        if (!IsValidConstructedTeamName(constructedTeam.Name))
            return null;

        if (!IsValidConstructedTeamAlias(constructedTeam.Alias))
            return null;

        using (await _constructedTeamLock.WaitAsync($"{constructedTeam.Id}", ct))
        {
            try
            {
                using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
                PlanetmansDbContext dbContext = factory.GetDbContext();

                dbContext.ConstructedTeams.Add(constructedTeam);

                await dbContext.SaveChangesAsync(ct);

                await SetUpConstructedTeamsMapAsync(ct);

                return constructedTeam;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create a constructed team");

                return null;
            }
        }
    }

    public async Task<CensusCharacter?> TryAddCharacterToConstructedTeamAsync
    (
        int teamId,
        string characterInput,
        string customAlias,
        CancellationToken ct = default
    )
    {
        Regex idRegex = new("^[0-9]{19}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (idRegex.Match(characterInput).Success)
        {
            try
            {
                CensusCharacter? characterOut = await TryAddCharacterIdToConstructedTeamAsync
                (
                    teamId,
                    ulong.Parse(characterInput),
                    customAlias,
                    ct
                );

                if (characterOut is not null)
                    return characterOut;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error trying to add character ID to constructed team");
            }
        }

        Regex nameRegex = new("^[A-Za-z0-9]{1,32}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (nameRegex.Match(characterInput).Success)
        {
            try
            {
                return await TryAddCharacterNameToConstructedTeamAsync(teamId, characterInput, customAlias, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error trying to add character name to constructed team");
            }
        }

        return null;
    }

    private async Task<CensusCharacter?> TryAddCharacterIdToConstructedTeamAsync
    (
        int teamId,
        ulong characterId,
        string customAlias,
        CancellationToken ct = default
    )
    {
        using (await _constructedTeamLock.WaitAsync($"{teamId}^{characterId}", ct))
        {
            if (await IsCharacterIdOnTeamAsync(teamId, characterId, ct))
                return null;

            CensusCharacter? character = await _characterService.GetByIdAsync(characterId, ct);
            if (character is null)
                return null;

            string playerAlias;
            if (string.IsNullOrWhiteSpace(customAlias))
            {
                playerAlias = Player.GetTrimmedPlayerName(character.Name.First, character.WorldId);
                if (string.IsNullOrWhiteSpace(playerAlias))
                    playerAlias = character.Name.First;
            }
            else
            {
                playerAlias = customAlias;
            }

            bool addedToDb = await TryAddCharacterToConstructedTeamDbAsync
            (
                teamId,
                characterId,
                (int)character.FactionId,
                playerAlias,
                ct
            );

            if (!addedToDb)
                return null;

            ConstructedTeamPlayerMembership member = new()
            {
                ConstructedTeamId = teamId,
                CharacterId = characterId,
                FactionId = (int)character.FactionId,
                Alias = playerAlias
            };

            ConstructedTeamMemberDetails memberDetails = ConvertToMemberDetailsModel(character, member);

            ConstructedTeamMemberChangeMessage changeMessage = new(teamId, character, memberDetails, ConstructedTeamMemberChangeType.Add);
            _messageService.BroadcastConstructedTeamMemberChangeMessage(changeMessage);

            return character;
        }
    }

    private async Task<CensusCharacter?> TryAddCharacterNameToConstructedTeamAsync
    (
        int teamId,
        string characterName,
        string customAlias,
        CancellationToken ct = default
    )
    {
        CensusCharacter? character = await _characterService.GetByNameAsync(characterName, ct);
        if (character is null)
            return null;

        using (await _constructedTeamLock.WaitAsync($"{teamId}^{character.CharacterId}", ct))
        {
            if (await IsCharacterIdOnTeamAsync(teamId, character.CharacterId, ct))
                return null;

            string playerAlias;
            if (string.IsNullOrWhiteSpace(customAlias))
            {
                playerAlias = Player.GetTrimmedPlayerName(character.Name.First, character.WorldId);

                if (string.IsNullOrWhiteSpace(playerAlias))
                    playerAlias = characterName;
            }
            else
            {
                playerAlias = customAlias;
            }

            bool addedToDb = await TryAddCharacterToConstructedTeamDbAsync
            (
                teamId,
                character.CharacterId,
                (int)character.FactionId,
                playerAlias,
                ct
            );

            if (!addedToDb)
                return null;

            ConstructedTeamPlayerMembership member = new()
            {
                ConstructedTeamId = teamId,
                CharacterId = character.CharacterId,
                FactionId = (int)character.FactionId,
                Alias = playerAlias
            };

            ConstructedTeamMemberDetails memberDetails = ConvertToMemberDetailsModel(character, member);

            ConstructedTeamMemberChangeMessage changeMessage = new(teamId, character, memberDetails, ConstructedTeamMemberChangeType.Add);
            _messageService.BroadcastConstructedTeamMemberChangeMessage(changeMessage);

            return character;
        }
    }

    private async Task<bool> TryAddCharacterToConstructedTeamDbAsync
    (
        int teamId,
        ulong characterId,
        int factionId,
        string alias,
        CancellationToken ct = default
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        // Don't allow NSO characters onto teams
        if (factionId is > 3 or <= 0)
            return false;

        ConstructedTeamPlayerMembership newEntity = new()
        {
            ConstructedTeamId = teamId,
            CharacterId = characterId,
            FactionId = factionId,
            Alias = alias
        };

        try
        {
            ConstructedTeamPlayerMembership? storeEntity = await dbContext.ConstructedTeamPlayerMemberships
                .Where(m => m.CharacterId == characterId && m.ConstructedTeamId == teamId)
                .FirstOrDefaultAsync(cancellationToken: ct);

            if (storeEntity != null)
            {
                return false;
            }

            dbContext.ConstructedTeamPlayerMemberships.Add(newEntity);

            await dbContext.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError
            (
                ex,
                "Error adding character ID {CharacterId} to team ID {TeamId} in database",
                characterId,
                teamId
            );

            return false;
        }
    }

    public async Task<bool> TryRemoveCharacterFromConstructedTeamAsync
    (
        int teamId,
        ulong characterId,
        CancellationToken ct = default
    )
    {
        using (await _constructedTeamLock.WaitAsync($"{teamId}^{characterId}", ct))
        {
            if (!await TryRemoveCharacterFromConstructedTeamDbAsync(teamId, characterId, ct))
                return false;

            ConstructedTeamMemberChangeMessage changeMessage = new(teamId, characterId, ConstructedTeamMemberChangeType.Remove);
            _messageService.BroadcastConstructedTeamMemberChangeMessage(changeMessage);

            return true;
        }
    }

    private async Task<bool> TryRemoveCharacterFromConstructedTeamDbAsync
    (
        int teamId,
        ulong characterId,
        CancellationToken ct = default
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        try
        {
            bool isMatchParticipant = await GetIsConstructedTeamMemberMatchParticipantAsync(teamId, characterId, ct);
            if (isMatchParticipant)
                return false;

            ConstructedTeamPlayerMembership? storeEntity = await dbContext.ConstructedTeamPlayerMemberships
                .FirstOrDefaultAsync
                (
                    m => m.CharacterId == characterId && m.ConstructedTeamId == teamId,
                    cancellationToken: ct
                );

            if (storeEntity == null)
                return false;

            dbContext.ConstructedTeamPlayerMemberships.Remove(storeEntity);

            await dbContext.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError
            (
                ex,
                "Error removing character ID {CharacterId} from team ID {TeamId} in database",
                characterId,
                teamId
            );

            return false;
        }
    }

    public async Task<bool> TryUpdateMemberAliasAsync
    (
        int teamId,
        ulong characterId,
        string oldAlias,
        string newAlias,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(newAlias) || !CharacterNameRegex.Match(newAlias).Success || oldAlias == newAlias)
        {
            return false;
        }

        using (await _constructedTeamLock.WaitAsync($"{teamId}^{characterId}", ct))
        {
            if (!await TryUpdateMemberAliasInDbAsync(teamId, characterId, newAlias, ct))
                return false;

            ConstructedTeamMemberChangeMessage changeMessage = new(teamId, characterId, ConstructedTeamMemberChangeType.UpdateAlias, oldAlias, newAlias);
            _messageService.BroadcastConstructedTeamMemberChangeMessage(changeMessage);

            return true;
        }
    }

    private async Task<bool> TryUpdateMemberAliasInDbAsync
    (
        int teamId,
        ulong characterId,
        string newAlias,
        CancellationToken ct
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        try
        {
            ConstructedTeamPlayerMembership? storeEntity = await dbContext.ConstructedTeamPlayerMemberships
                .FirstOrDefaultAsync
                (
                    m => m.CharacterId == characterId && m.ConstructedTeamId == teamId,
                    cancellationToken: ct
                );

            if (storeEntity == null)
            {
                return false;
            }

            storeEntity.Alias = newAlias;

            dbContext.ConstructedTeamPlayerMemberships.Update(storeEntity);

            await dbContext.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            string newAliasDisplay = string.IsNullOrWhiteSpace(newAlias) ? "null" : newAlias;

            _logger.LogError
            (
                ex,
                "Error updating alias to {NewAlias} for character ID {CharacterId} on team ID {TeamId} in database",
                newAliasDisplay,
                characterId,
                teamId
            );

            return false;
        }
    }
    #endregion CREATE / EDIT Methods

    public async Task SetUpConstructedTeamsMapAsync(CancellationToken ct = default)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            List<ConstructedTeam> teams = await dbContext.ConstructedTeams.ToListAsync(cancellationToken: ct);

            foreach (int teamId in _constructedTeamsMap.Keys)
            {
                if (teams.All(t => t.Id != teamId))
                    _constructedTeamsMap.TryRemove(teamId, out _);
            }

            foreach (ConstructedTeam team in teams)
            {
                if (_constructedTeamsMap.ContainsKey(team.Id))
                {
                    _constructedTeamsMap[team.Id] = team;
                }
                else
                {
                    _constructedTeamsMap.TryAdd(team.Id, team);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed setting up ConstructedTeamsMap");
        }
    }

    public async Task<bool> IsCharacterIdOnTeamAsync
    (
        int teamId,
        ulong characterId,
        CancellationToken ct = default
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ConstructedTeamPlayerMemberships.AnyAsync
        (
            m => m.CharacterId == characterId && m.ConstructedTeamId == teamId,
            cancellationToken: ct
        );
    }

    private async Task SetConstructedTeamMemberIsMatchParticipantAsync
    (
        ConstructedTeamMemberDetails memberDetails,
        CancellationToken ct = default
    )
    {
        memberDetails.IsMatchParticipant = await GetIsConstructedTeamMemberMatchParticipantAsync
        (
            memberDetails.ConstructedTeamId,
            memberDetails.CharacterId,
            ct
        );
    }

    public async Task<bool> GetIsConstructedTeamMemberMatchParticipantAsync
    (
        int teamId,
        ulong characterId,
        CancellationToken ct = default
    )
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ScrimMatchParticipatingPlayers.AnyAsync
        (
            p => p.IsFromConstructedTeam
                && p.ConstructedTeamId == teamId
                && p.CharacterId == characterId,
            cancellationToken: ct
        );
    }

    public async Task<bool> GetIsConstructedTeamMatchParticipant(int teamId)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ScrimMatchParticipatingPlayers.AnyAsync(p => p.IsFromConstructedTeam
            && p.ConstructedTeamId == teamId);
    }

    public static bool IsValidConstructedTeamName(string name)
    {
        return ConstructedTeamNameRegex.Match(name).Success;
    }

    public static bool IsValidConstructedTeamAlias(string alias)
    {
        return ConstructedTeamAliasRegex.Match(alias).Success;
    }
}
