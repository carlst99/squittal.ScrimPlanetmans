using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Extensions;

public static class ScrimActionTypeExtensions
{
    public static ScrimActionTypeDomain GetDomain(this ScrimActionType action)
    {
        return (int)action switch
        {
            >= 10 and < 100 => ScrimActionTypeDomain.Objective,
            >= 300 and < 399 => ScrimActionTypeDomain.Support,
            >= 100 and < 200 => ScrimActionTypeDomain.Infantry,
            >= 200 and < 300 => ScrimActionTypeDomain.MAX,
            >= 400 and < 500 => ScrimActionTypeDomain.Vehicle,
            >= 500 and < 1999 => ScrimActionTypeDomain.AirVehicle,
            >= 2000 and < 2999 => ScrimActionTypeDomain.GroundVehicle,
            _ => ScrimActionTypeDomain.Other
        };
    }
}
