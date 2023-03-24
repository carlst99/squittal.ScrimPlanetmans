using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DbgCensus.Core.Objects;
using squittal.ScrimPlanetmans.App.Data.Models;

namespace squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;

public class ScrimMatchInfo
{
    [Required]
    public string ScrimMatchId { get; init; }
    public DateTime StartTime { get; set; }
    public string Title { get; set; }

    public Dictionary<TeamDefinition, string> TeamAliases { get; set; } = new()
    {
        { TeamDefinition.Team1, "???" },
        { TeamDefinition.Team2, "???" }
    };

    public int RoundCount { get; set; }

    // World & Facility correspond to last round's configuration
    public WorldDefinition WorldId { get; set; }
    public uint? FacilityId { get; set; }

    //public bool EndRoundOnFacilityCapture { get; set; } = false;
    public FactionDefinition TeamOneFactionId { get; set; }
    public FactionDefinition TeamTwoFactionId { get; set; }

    public int RulesetId { get; set; }
    public string RulesetName { get; set; }


    public ScrimMatchInfo()
    {

    }

    public ScrimMatchInfo(Data.Models.ScrimMatch scrimMatch, ScrimMatchRoundConfiguration lastRoundConfiguration)
    {
        ScrimMatchId = scrimMatch.Id;
        StartTime = scrimMatch.StartTime;
        Title = scrimMatch.Title;

        SetTeamAliases();

        RoundCount = lastRoundConfiguration.ScrimMatchRound;
        WorldId = lastRoundConfiguration.WorldId;
        FacilityId = lastRoundConfiguration.FacilityId;
    }

    public void SetTeamAliases()
    {
        if (string.IsNullOrWhiteSpace(ScrimMatchId))
            return;

        string[] idParts = ScrimMatchId.Split("_");
        TeamAliases[TeamDefinition.Team1] = idParts[1];
        TeamAliases[TeamDefinition.Team2] = idParts[2];
    }
}
