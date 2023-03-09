using System;
using DbgCensus.EventStream;
using DbgCensus.EventStream.EventHandlers.Extensions;
using DbgCensus.Rest;
using DbgCensus.Rest.Extensions;
using DbgCensus.Rest.Objects;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.CensusEventStreamHandlers;
using squittal.ScrimPlanetmans.App.CensusEventStreamHandlers.Control;
using squittal.ScrimPlanetmans.App.CensusEventStreamHandlers.PreDispatch;
using squittal.ScrimPlanetmans.App.CensusServices;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset;
using squittal.ScrimPlanetmans.App.Services;
using squittal.ScrimPlanetmans.App.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Services.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Planetside;
using squittal.ScrimPlanetmans.App.Services.Planetside.Interfaces;
using squittal.ScrimPlanetmans.App.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Services.Rulesets.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.Services.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.Services.ScrimMatchReports.Interfaces;
using squittal.ScrimPlanetmans.App.Workers;

namespace squittal.ScrimPlanetmans.App;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Serilog.AspNetCore.RequestLoggingMiddleware", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console
            (
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        try
        {
            BuildAndRun(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void BuildAndRun(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();
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

        services.AddTransient<DbInitializerService>();
        services.AddSingleton<IDbContextHelper, DbContextHelper>();
        services.AddSingleton<IDbSeeder, DbSeeder>();
        services.AddTransient<ISqlScriptRunner, SqlScriptRunner>();
        services.AddTransient<DatabaseMaintenanceService>();

        // Register Census REST services
        builder.Services.Configure<CensusQueryOptions>(builder.Configuration.GetSection(nameof(CensusQueryOptions)))
            .Configure<CensusQueryOptions>(o => o.LanguageCode = CensusLanguage.English);

        services.AddCensusRestServices()
            .AddTransient<ICensusCharacterService, CensusCharacterService>()
            .AddTransient<ICensusLoadoutService, CensusLoadoutService>()
            .AddTransient<ICensusOutfitService, CensusOutfitService>()
            .AddTransient<ICensusProfileService, CensusProfileService>();

        services.AddCensusServices
        (
            options => options.CensusServiceId = builder.Configuration["DaybreakGamesServiceKey"]
        );
        services.AddCensusHelpers();

        // Register Census helper services
        services.AddTransient<ILoadoutService, LoadoutService>();
        services.AddSingleton<IZoneService, ZoneService>();
        services.AddSingleton<IItemService, ItemService>();
        services.AddSingleton<IItemCategoryService, ItemCategoryService>();
        services.AddSingleton<IFacilityService, FacilityService>();
        services.AddTransient<IVehicleService, VehicleService>();
        services.AddSingleton<IWorldService, WorldService>();
        services.AddSingleton<IOutfitService, OutfitService>();

        // Register Census EventStream Services
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

        services.AddSingleton<IEventFilterService, EventFilterService>();
        services.AddSingleton<IEventStreamHealthService, EventStreamHealthService>();

        services.AddHostedService<EventStreamWorker>();

        // Register Scrim services
        services.AddSingleton<IScrimMessageBroadcastService, ScrimMessageBroadcastService>();
        services.AddSingleton<IScrimRulesetManager, ScrimRulesetManager>();
        services.AddSingleton<IScrimMatchDataService, ScrimMatchDataService>();

        services.AddSingleton<IScrimTeamsManager, ScrimTeamsManager>();
        services.AddSingleton<IScrimPlayersService, ScrimPlayersService>();

        services.AddSingleton<IStatefulTimer, StatefulTimer>();
        services.AddSingleton<IScrimMatchEngine, ScrimMatchEngine>();
        services.AddSingleton<IScrimMatchScorer, ScrimMatchScorer>();

        services.AddTransient<IDeathEventTypeService, DeathEventTypeService>();
        services.AddTransient<IVehicleTypeService, VehicleTypeService>();
        services.AddSingleton<IConstructedTeamService, ConstructedTeamService>();

        services.AddSingleton<IRulesetDataService, RulesetDataService>();

        services.AddTransient<IScrimMatchReportDataService, ScrimMatchReportDataService>();

        services.AddSingleton<IApplicationDataLoader, ApplicationDataLoader>();
        services.AddHostedService<ApplicationDataLoaderHostedService>();

        WebApplication app = builder.Build();

        app.UseSerilogRequestLogging();

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

        if (!InitializeDatabase(app.Services))
            return;

        app.Run();
    }

    private static bool InitializeDatabase(IServiceProvider serviceProvider)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        return scope.ServiceProvider
            .GetRequiredService<DbInitializerService>()
            .Initialize();
    }
}
