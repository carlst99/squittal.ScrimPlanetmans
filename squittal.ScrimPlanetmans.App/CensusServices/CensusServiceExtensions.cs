using Microsoft.Extensions.DependencyInjection;

namespace squittal.ScrimPlanetmans.App.CensusServices;

public static class CensusServiceExtensions
{
    public static IServiceCollection AddCensusHelpers(this IServiceCollection services)
    {
        services.AddSingleton<CensusFaction>();
        services.AddSingleton<CensusFacility>();
        services.AddSingleton<CensusItem>();
        services.AddSingleton<CensusItemCategory>();
        services.AddSingleton<CensusVehicle>();
        services.AddSingleton<CensusWorld>();
        services.AddSingleton<CensusZone>();

        return services;
    }
}
