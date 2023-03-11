using System;
using System.Text.RegularExpressions;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Services.Planetside;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Models;

public class Player
{
    public ulong Id { get; }

    public TeamDefinition TeamOrdinal { get; set; }

    public ScrimEventAggregate EventAggregate => EventAggregateTracker.TotalStats;
    public ScrimEventAggregateRoundTracker EventAggregateTracker { get; set; } = new();

    public string NameFull { get; }
    public string NameTrimmed { get; private set; }
    public string? NameAlias { get; private set; }

    public string NameDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(NameAlias))
                return NameAlias;

            return !string.IsNullOrWhiteSpace(NameTrimmed)
                ? NameTrimmed
                : NameFull;
        }
    }

    public int FactionId { get; set; }
    public int WorldId { get; set; }

    public int PrestigeLevel { get; set; }

    public ulong? OutfitId { get; set; }
    public string? OutfitAlias { get; set; }
    public string? OutfitAliasLower { get; set; }
    public bool IsOutfitless { get; set; }

    public int? ConstructedTeamId { get; set; }
    public bool IsFromConstructedTeam => ConstructedTeamId != null;

    // Dynamic Attributes
    public int? LoadoutId { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Unknown;

    public bool IsOnline { get; set; }
    public bool IsActive { get; set; }
    public bool IsParticipating { get; set; }
    public bool IsBenched { get; set; }

    public bool IsVisibleInTeamComposer => GetIsVisibleInTeamComposer();

    public bool IsAdHocPlayer => GetIsAdHocPlayer();

    private static readonly Regex _nameRegex = new Regex("^[A-Za-z0-9]{1,32}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Format for Planetside Infantry League: Season 2 => Namex##
    private static readonly Regex _pil2NameRegex = new Regex("^[A-z0-9]{2,}(x[0-9]{2})$", RegexOptions.Compiled);

    // Format for Legacy Jaeger Characters => TAGxName(VS|NC|TR)
    private static readonly Regex _legacyJaegerNameRegex = new("^([A-z0-9]{0,4}x).{2,}(?<!(x[0-9]{2}))$", RegexOptions.Compiled);

    private static readonly Regex _factionSufficRegex = new("^[A-z0-9]+(VS|NC|TR)$", RegexOptions.Compiled);

    public Player(CensusCharacter character, bool isOnline)
    {
        Id = character.CharacterId;
        NameFull = character.Name.First;
        IsOnline = isOnline;
        PrestigeLevel = character.PrestigeLevel;
        FactionId = (int)character.FactionId;
        WorldId = (int)character.WorldId;

        IsOutfitless = character.Outfit is null;
        if (character.Outfit is not null)
        {
            OutfitId = character.Outfit.OutfitId;
            OutfitAlias = character.Outfit.Alias;
            OutfitAliasLower = OutfitAlias.ToLower();
        }

        NameTrimmed = GetTrimmedPlayerName(NameFull, WorldId);
    }

    #region Temporary Alias
    public static string GetTrimmedPlayerName(string name, int worldId)
    {
        bool isPil2NameFormat = false;
        bool isLegacyJaegerNameFormat = false;

        if (WorldService.IsJaegerWorldId(worldId))
        {
            if (_pil2NameRegex.Match(name).Success)
            {
                isPil2NameFormat = true;
            }
            else if (_legacyJaegerNameRegex.Match(name).Success)
            {
                isLegacyJaegerNameFormat = true;
            }
        }

        string trimmed = name;
        int initLength = name.Length;

        if (isPil2NameFormat)
        {
            trimmed = name[..(initLength - 3)];
        }
        else if (isLegacyJaegerNameFormat)
        {
            // Remove outfit tag from beginning of name
            int idx = name.IndexOf("x", StringComparison.Ordinal);
            if (idx is >= 0 and < 5 && idx != initLength - 1)
            {
                trimmed = name.Substring(idx + 1, initLength - idx - 1);
            }
        }

        if (!isPil2NameFormat && _factionSufficRegex.Match(trimmed).Success)
        {
            // Remove faction abbreviation from end of name
            int end = trimmed.Length - 2;
            trimmed = trimmed[..end];
        }

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length <= 1)
        {
            trimmed = name;
        }

        return trimmed;
    }

    public void UpdateNameTrimmed()
    {
        NameTrimmed = GetTrimmedPlayerName(NameFull, WorldId);
    }

    public bool TrySetNameAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return false;

        Match match = _nameRegex.Match(alias);
        if (!match.Success)
            return false;

        NameAlias = alias;
        return true;
    }

    public void ClearAllDisplayNameSources()
    {
        NameAlias = string.Empty;
        NameTrimmed = string.Empty;
    }
    #endregion Temporary Alias

    #region Event Aggregate & Stat Updates
    public void AddStatsUpdate(ScrimEventAggregate update)
    {
        EventAggregateTracker.AddToCurrent(update);
    }

    public void SubtractStatsUpdate(ScrimEventAggregate update)
    {
        EventAggregateTracker.SubtractFromCurrent(update);
    }

    public void ClearEventAggregateHistory()
    {
        EventAggregateTracker = new ScrimEventAggregateRoundTracker();
    }
    #endregion Event Aggregate & Stat Updates

    public void ResetMatchData()
    {
        ClearEventAggregateHistory();

        LoadoutId = null;
        Status = PlayerStatus.Unknown;
        IsActive = false;
        IsParticipating = false;
    }

    private bool GetIsVisibleInTeamComposer()
    {
        if (IsParticipating || IsOnline)
        {
            return true;
        }
        else if (IsAdHocPlayer)
        {
            return true;
        }
        else if (IsFromConstructedTeam)
        {
            return true;
        }
        else if (!IsOutfitless)
        {
            return false;
        }
        else
        {
            return false;
        }
    }


    private bool GetIsAdHocPlayer()
    {
        if (IsFromConstructedTeam || !IsOutfitless)
        {
            return false;
        }
        else
        {
            return true;
        }
    }
    #region Eqaulity

    public override bool Equals(object? obj)
        => obj is Player player
            && Equals(player);

    public bool Equals(Player? p)
        => p is not null
            && p.Id == Id;

    public static bool operator ==(Player? lhs, Player? rhs)
    {
        if (lhs is not null && rhs is not null)
            return lhs.Equals(rhs);

        return lhs is null && rhs is null;
    }

    public static bool operator !=(Player? lhs, Player? rhs)
    {
        return !(lhs == rhs);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    #endregion Eqaulity
}


public enum PlayerStatus
{
    Unknown,
    Alive,
    Respawning,
    Revived,
    ContestingObjective,
    Benched,
    Offline
}
