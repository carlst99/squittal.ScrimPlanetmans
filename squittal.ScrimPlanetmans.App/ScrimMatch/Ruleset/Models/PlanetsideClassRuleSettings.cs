using squittal.ScrimPlanetmans.App.Models.CensusRest;

namespace squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

public class PlanetsideClassRuleSettings
{
    public bool InfiltratorIsBanned { get; set; }
    public int InfiltratorPoints { get; set; }

    public bool LightAssaultIsBanned { get; set; }
    public int LightAssaultPoints { get; set; }

    public bool MedicIsBanned { get; set; }
    public int MedicPoints { get; set; }

    public bool EngineerIsBanned { get; set; }
    public int EngineerPoints { get; set; }

    public bool HeavyAssaultIsBanned { get; set; }
    public int HeavyAssaultPoints { get; set; }

    public bool MaxIsBanned { get; set; }
    public int MaxPoints { get; set; }

    public PlanetsideClassRuleSettings()
    {
    }

    public PlanetsideClassRuleSettings(RulesetItemCategoryRule rule)
    {
        InfiltratorIsBanned = rule.InfiltratorIsBanned;
        InfiltratorPoints = rule.InfiltratorPoints;
        LightAssaultIsBanned = rule.LightAssaultIsBanned;
        LightAssaultPoints = rule.LightAssaultPoints;
        MedicIsBanned = rule.MedicIsBanned;
        MedicPoints = rule.MedicPoints;
        EngineerIsBanned = rule.EngineerIsBanned;
        EngineerPoints = rule.EngineerPoints;
        HeavyAssaultIsBanned = rule.HeavyAssaultIsBanned;
        HeavyAssaultPoints = rule.HeavyAssaultPoints;
        MaxIsBanned = rule.MaxIsBanned;
        MaxPoints = rule.MaxPoints;
    }

    public PlanetsideClassRuleSettings(RulesetItemRule rule)
    {
        InfiltratorIsBanned = rule.InfiltratorIsBanned;
        InfiltratorPoints = rule.InfiltratorPoints;
        LightAssaultIsBanned = rule.LightAssaultIsBanned;
        LightAssaultPoints = rule.LightAssaultPoints;
        MedicIsBanned = rule.MedicIsBanned;
        MedicPoints = rule.MedicPoints;
        EngineerIsBanned = rule.EngineerIsBanned;
        EngineerPoints = rule.EngineerPoints;
        HeavyAssaultIsBanned = rule.HeavyAssaultIsBanned;
        HeavyAssaultPoints = rule.HeavyAssaultPoints;
        MaxIsBanned = rule.MaxIsBanned;
        MaxPoints = rule.MaxPoints;
    }

    public PlanetsideClassRuleSettings(JsonRulesetItemCategoryRule rule)
    {
        InfiltratorIsBanned = rule.InfiltratorIsBanned;
        InfiltratorPoints = rule.InfiltratorPoints;
        LightAssaultIsBanned = rule.LightAssaultIsBanned;
        LightAssaultPoints = rule.LightAssaultPoints;
        MedicIsBanned = rule.MedicIsBanned;
        MedicPoints = rule.MedicPoints;
        EngineerIsBanned = rule.EngineerIsBanned;
        EngineerPoints = rule.EngineerPoints;
        HeavyAssaultIsBanned = rule.HeavyAssaultIsBanned;
        HeavyAssaultPoints = rule.HeavyAssaultPoints;
        MaxIsBanned = rule.MaxIsBanned;
        MaxPoints = rule.MaxPoints;
    }

    public PlanetsideClassRuleSettings(JsonRulesetItemRule rule)
    {
        InfiltratorIsBanned = rule.InfiltratorIsBanned;
        InfiltratorPoints = rule.InfiltratorPoints;
        LightAssaultIsBanned = rule.LightAssaultIsBanned;
        LightAssaultPoints = rule.LightAssaultPoints;
        MedicIsBanned = rule.MedicIsBanned;
        MedicPoints = rule.MedicPoints;
        EngineerIsBanned = rule.EngineerIsBanned;
        EngineerPoints = rule.EngineerPoints;
        HeavyAssaultIsBanned = rule.HeavyAssaultIsBanned;
        HeavyAssaultPoints = rule.HeavyAssaultPoints;
        MaxIsBanned = rule.MaxIsBanned;
        MaxPoints = rule.MaxPoints;
    }

    public bool GetClassIsBanned(CensusProfileType planetsideClass)
    {
        return planetsideClass switch
        {
            CensusProfileType.Infiltrator => InfiltratorIsBanned,
            CensusProfileType.LightAssault => LightAssaultIsBanned,
            CensusProfileType.CombatMedic => MedicIsBanned,
            CensusProfileType.Engineer => EngineerIsBanned,
            CensusProfileType.HeavyAssault => HeavyAssaultIsBanned,
            CensusProfileType.MAX => MaxIsBanned,
            _ => false,
        };
    }

    public int GetClassPoints(CensusProfileType planetsideClass)
    {
        return planetsideClass switch
        {
            CensusProfileType.Infiltrator => InfiltratorPoints,
            CensusProfileType.LightAssault => LightAssaultPoints,
            CensusProfileType.CombatMedic => MedicPoints,
            CensusProfileType.Engineer => EngineerPoints,
            CensusProfileType.HeavyAssault => HeavyAssaultPoints,
            CensusProfileType.MAX => MaxPoints,
            _ => 0,
        };
    }

    public void SetClassIsBanned(CensusProfileType planetsideClass, bool newIsBanned)
    {
        switch (planetsideClass)
        {
            case CensusProfileType.Infiltrator:
                InfiltratorIsBanned = newIsBanned;
                return;

            case CensusProfileType.LightAssault:
                LightAssaultIsBanned = newIsBanned;
                return;

            case CensusProfileType.CombatMedic:
                MedicIsBanned = newIsBanned;
                return;

            case CensusProfileType.Engineer:
                EngineerIsBanned = newIsBanned;
                return;

            case CensusProfileType.HeavyAssault:
                HeavyAssaultIsBanned = newIsBanned;
                return;

            case CensusProfileType.MAX:
                MaxIsBanned = newIsBanned;
                return;

            default:
                return;
        }
    }

    public void SetClassPoints(CensusProfileType planetsideClass, int newPoints)
    {
        switch (planetsideClass)
        {
            case CensusProfileType.Infiltrator:
                InfiltratorPoints = newPoints;
                return;

            case CensusProfileType.LightAssault:
                LightAssaultPoints = newPoints;
                return;

            case CensusProfileType.CombatMedic:
                MedicPoints = newPoints;
                return;

            case CensusProfileType.Engineer:
                EngineerPoints = newPoints;
                return;

            case CensusProfileType.HeavyAssault:
                HeavyAssaultPoints = newPoints;
                return;

            case CensusProfileType.MAX:
                MaxPoints = newPoints;
                return;

            default:
                return;
        }
    }

    public override bool Equals(object? obj)
        => obj is PlanetsideClassRuleSettings settings
            && Equals(settings);

    public bool Equals(PlanetsideClassRuleSettings? s)
    {
        if (s is null)
            return false;

        if (ReferenceEquals(this, s))
            return true;

        return this.InfiltratorIsBanned == s.InfiltratorIsBanned
            && this.InfiltratorPoints == s.InfiltratorPoints
            && this.LightAssaultIsBanned == s.LightAssaultIsBanned
            && this.LightAssaultPoints == s.LightAssaultPoints
            && this.MedicIsBanned == s.MedicIsBanned
            && this.MedicPoints == s.MedicPoints
            && this.EngineerIsBanned == s.EngineerIsBanned
            && this.EngineerPoints == s.EngineerPoints
            && this.HeavyAssaultIsBanned == s.HeavyAssaultIsBanned
            && this.HeavyAssaultPoints == s.HeavyAssaultPoints
            && this.MaxIsBanned == s.MaxIsBanned
            && this.MaxPoints == s.MaxPoints;
    }

    public static bool operator ==(PlanetsideClassRuleSettings? lhs, PlanetsideClassRuleSettings? rhs)
    {
        if (lhs is null)
            return rhs is null;

        return lhs.Equals(rhs);
    }

    public static bool operator !=(PlanetsideClassRuleSettings? lhs, PlanetsideClassRuleSettings? rhs)
    {
        return !(lhs == rhs);
    }
}
