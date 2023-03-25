using System.Collections.Generic;
using System.Linq;
using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;

public class ScrimMatchReportInfantryPlayerClassEventCounts
{
    public string ScrimMatchId { get; set; }
    public int TeamOrdinal { get; set; }
    public string CharacterId { get; set; }
    public string NameDisplay { get; set; }
    public int FactionId { get; set; }
    public int PrestigeLevel { get; set; }
    public int EventsAsHeavyAssault { get; set; }
    public int EventsAsInfiltrator { get; set; }
    public int EventsAsLightAssault { get; set; }
    public int EventsAsMedic { get; set; }
    public int EventsAsEngineer { get; set; }
    public int EventsAsMax { get; set; }
    public int KillsAsHeavyAssault { get; set; }
    public int KillsAsInfiltrator { get; set; }
    public int KillsAsLightAssault { get; set; }
    public int KillsAsMedic { get; set; }
    public int KillsAsEngineer { get; set; }
    public int KillsAsMax { get; set; }
    public int DeathsAsHeavyAssault { get; set; }
    public int DeathsAsInfiltrator { get; set; }
    public int DeathsAsLightAssault { get; set; }
    public int DeathsAsMedic { get; set; }
    public int DeathsAsEngineer { get; set; }
    public int DeathsAsMax { get; set; }
    public int DamageAssistsAsHeavyAssault { get; set; }
    public int DamageAssistsAsInfiltrator { get; set; }
    public int DamageAssistsAsLightAssault { get; set; }
    public int DamageAssistsAsMedic { get; set; }
    public int DamageAssistsAsEngineer { get; set; }
    public int DamageAssistsAsMax { get; set; }

    public CensusProfileType PrimaryPlanetsideClass => GetOrderedPlanetsideClassEventCountsList()
        .First()
        .PlanetsideClass;

    public IEnumerable<PlanetsideClassEventCount> GetOrderedPlanetsideClassEventCountsList()
    {
        List<PlanetsideClassEventCount> classCountsList = new()
        {
            new(CensusProfileType.HeavyAssault, EventsAsHeavyAssault),
            new(CensusProfileType.LightAssault, EventsAsLightAssault),
            new(CensusProfileType.Infiltrator, EventsAsInfiltrator),
            new(CensusProfileType.CombatMedic, EventsAsMedic),
            new(CensusProfileType.Engineer, EventsAsEngineer),
            new(CensusProfileType.MAX, EventsAsMax)
        };

        return classCountsList.OrderByDescending(c => c.EventCount);
    }
}
