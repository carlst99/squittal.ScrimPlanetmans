using System;
using DbgCensus.EventStream;
using DbgCensus.EventStream.EventHandlers.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.CensusStream;
using squittal.ScrimPlanetmans.App.CensusStream.EventHandlers;
using squittal.ScrimPlanetmans.App.CensusStream.EventHandlers.Control;
using squittal.ScrimPlanetmans.App.CensusStream.EventHandlers.PreDispatch;
using squittal.ScrimPlanetmans.App.CensusStream.Interfaces;
using squittal.ScrimPlanetmans.App.CensusStream.Models;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset;
using squittal.ScrimPlanetmans.App.Services;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Services.Rulesets.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.Services.ScrimMatchReports.Interfaces;

namespace squittal.ScrimPlanetmans.App;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSystemd();

        IServiceCollection services = builder.Services;

        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddSignalR();

        // Register the database
        services.AddDbContext<PlanetmansDbContext>
        (
            options => options.UseSqlServer
                (
                    builder.Configuration.GetConnectionString("PlanetmansDbContext"),
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure
                        (
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null
                        );
                    }
                )
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
        );

        // Register internal Census services
        services.AddCensusServices
        (
            options => options.CensusServiceId = Environment.GetEnvironmentVariable
                (
                    "DaybreakGamesServiceKey",
                    EnvironmentVariableTarget.User
                )
        );
        services.AddCensusHelpers();

        // Register DbgCensus services
        builder.Services.Configure<EventStreamOptions>(builder.Configuration.GetSection(nameof(EventStreamOptions)));

        services.AddCensusEventHandlingServices()
            .RegisterPreDispatchHandler<EventFilterPreDispatchHandler>()
            .RegisterPreDispatchHandler<DuplicatePreventionPreDispatchHandler>()
            .AddPayloadHandler<ConnectionStateChangedPayloadHandler>()
            .AddPayloadHandler<DeathEventHandler>()
            .AddPayloadHandler<FacilityControlEventHandler>()
            .AddPayloadHandler<GainExperiencePayloadHandler>()
            .AddPayloadHandler<PlayerLogEventHandler>()
            .AddPayloadHandler<VehicleDestroyEventHandler>()
            .AddPayloadHandler<UnknownPayloadHandler>();

        services.AddHostedService<EventStreamWorker>();

        services.AddSingleton<IDbContextHelper, DbContextHelper>();

        services.AddSingleton<IEventFilterService, EventFilterService>();
        services.AddSingleton<IScrimMessageBroadcastService, ScrimMessageBroadcastService>();

        services.AddTransient<IFactionService, FactionService>();
        services.AddSingleton<IZoneService, ZoneService>();

        services.AddSingleton<IItemService, ItemService>();
        services.AddSingleton<IItemCategoryService, ItemCategoryService>();
        services.AddSingleton<IFacilityService, FacilityService>();
        services.AddTransient<IFacilityTypeService, FacilityTypeService>();
        services.AddTransient<IVehicleService, VehicleService>();

        services.AddTransient<IVehicleTypeService, VehicleTypeService>();
        services.AddTransient<IDeathEventTypeService, DeathEventTypeService>();

        services.AddSingleton<IScrimRulesetManager, ScrimRulesetManager>();

        services.AddSingleton<IScrimMatchDataService, ScrimMatchDataService>();

        services.AddSingleton<IWorldService, WorldService>();
        services.AddSingleton<ICharacterService, CharacterService>();
        services.AddSingleton<IOutfitService, OutfitService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddTransient<ILoadoutService, LoadoutService>();

        services.AddSingleton<IScrimTeamsManager, ScrimTeamsManager>();
        services.AddSingleton<IScrimPlayersService, ScrimPlayersService>();

        services.AddSingleton<IStatefulTimer, StatefulTimer>();
        services.AddSingleton<IScrimMatchEngine, ScrimMatchEngine>();
        services.AddSingleton<IScrimMatchScorer, ScrimMatchScorer>();

        services.AddSingleton<IConstructedTeamService, ConstructedTeamService>();

        services.AddSingleton<IRulesetDataService, RulesetDataService>();

        services.AddTransient<IScrimMatchReportDataService, ScrimMatchReportDataService>();

        services.AddTransient<IStreamClient, StreamClient>();
        services.AddSingleton<IEventStreamHealthService, EventStreamHealthService>();

        services.AddSingleton<IApplicationDataLoader, ApplicationDataLoader>();
        services.AddHostedService<ApplicationDataLoaderHostedService>();

        services.AddSingleton<IDbSeeder, DbSeeder>();
        services.AddTransient<ISqlScriptRunner, SqlScriptRunner>();
        services.AddTransient<DatabaseMaintenanceService>();

        WebApplication app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        InitializeDatabase(app.Services);

        app.Run();
    }

    private static void InitializeDatabase(IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        try
        {
            DbInitializer.Initialize(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occured initializing the DB");
        }
    }
}
