﻿namespace squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;

public class ScrimMatchReportInfantryPlayerHeadToHeadStats : ScrimMatchReportInfantryHeadToHeadStats
{
    public string ScrimMatchId { get; set; }
    public TeamDefinition PlayerTeamOrdinal { get; set; }
    public string PlayerCharacterId { get; set; }
    public string PlayerNameDisplay { get; set; }
    public int PlayerFactionId { get; set; }
    public int PlayerPrestigeLevel { get; set; }
    public TeamDefinition OpponentTeamOrdinal { get; set; }
    public string OpponentCharacterId { get; set; }
    public string OpponentNameDisplay { get; set; }
    public string OpponentNameFull { get; set; }
    public int OpponentFactionId { get; set; }
    public int OpponentPrestigeLevel { get; set; }
}
