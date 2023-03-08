using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.CensusRest;
using squittal.ScrimPlanetmans.App.Models.Forms;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Events;

public class ConstructedTeamMemberChangeMessage
{
    public CensusCharacter? Character { get; }
    public ConstructedTeamMemberDetails? MemberDetails { get; }
    public ulong CharacterId { get; }
    public int TeamId { get; }
    public ConstructedTeamMemberChangeType ChangeType { get; }
    public string? MemberAlias { get; }
    public string Info { get; set; } // => GetInfoMessage(); }

    // ADD Message
    public ConstructedTeamMemberChangeMessage
    (
        int teamId,
        CensusCharacter character,
        ConstructedTeamMemberDetails memberDetails,
        ConstructedTeamMemberChangeType changeType
    )
    {
        Character = character;
        CharacterId = character.CharacterId;
        MemberDetails = memberDetails;
        TeamId = teamId;
        ChangeType = changeType;
        MemberAlias = memberDetails.NameAlias;

        Info = GetInfoMessage();
    }

    // REMOVE Message
    public ConstructedTeamMemberChangeMessage(int teamId, ulong characterId, ConstructedTeamMemberChangeType changeType)
    {
        CharacterId = characterId;
        TeamId = teamId;
        ChangeType = changeType;

        Info = GetInfoMessage();
    }

    // UPDATE ALIAS Message
    public ConstructedTeamMemberChangeMessage
    (
        int teamId,
        ulong characterId,
        ConstructedTeamMemberChangeType changeType,
        string oldAlias,
        string newAlias
    )
    {
        CharacterId = characterId;
        TeamId = teamId;
        ChangeType = changeType;

        MemberAlias = newAlias;

        string type = ChangeType.ToString().ToUpper();

        string oldAliasDisplay = string.IsNullOrWhiteSpace(oldAlias) ? "null" : oldAlias;
        string newAliasDisplay = string.IsNullOrWhiteSpace(newAlias) ? "null" : newAlias;

        Info = $"Constructed Team {TeamId} Character {type}: {oldAliasDisplay} => {newAliasDisplay} [{CharacterId}]";
    }

    private string GetInfoMessage()
    {
        string characterName = string.Empty;
        if (Character is not null)
            characterName = Character.Name.First;

        string type = ChangeType.ToString().ToUpper();

        return $"Constructed Team {TeamId} Character {type}: {characterName} [{CharacterId}]";
    }
}
