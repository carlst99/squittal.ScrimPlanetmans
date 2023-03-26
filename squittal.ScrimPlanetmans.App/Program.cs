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
using squittal.ScrimPlanetmans.App.Abstractions.Services;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Abstractions.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Planetside;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.CensusEventStreamHandlers;
using squittal.ScrimPlanetmans.App.CensusEventStreamHandlers.Control;
using squittal.ScrimPlanetmans.App.CensusEventStreamHandlers.PreDispatch;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch;
using squittal.ScrimPlanetmans.App.ScrimMatch.Interfaces;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset;
using squittal.ScrimPlanetmans.App.Services;
using squittal.ScrimPlanetmans.App.Services.CensusEventStream;
using squittal.ScrimPlanetmans.App.Services.CensusRest;
using squittal.ScrimPlanetmans.App.Services.Planetside;
using squittal.ScrimPlanetmans.App.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Workers;

namespace squittal.ScrimPlanetmans.App;

public class Program
{
    public static void Main(string[] args)
    {
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
        builder.Host.UseSystemd();

        SetupLogger(builder.Configuration, builder.Environment);
        builder.Host.UseSerilog();

        IServiceCollection services = builder.Services;

        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<Program>());

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
                    .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()),
                optionsLifetime: ServiceLifetime.Singleton
            )
            .AddDbContextFactory<PlanetmansDbContext>
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
        services.AddTransient<ISqlScriptService, SqlScriptService>();

        // Register Census REST services
        builder.Services.Configure<CensusQueryOptions>(builder.Configuration.GetSection(nameof(CensusQueryOptions)))
            .Configure<CensusQueryOptions>(o => o.LanguageCode = CensusLanguage.English)
            .Configure<CensusQueryOptions>(o => o.Limit = 50);

        // Configure a second query options to point towards Sanctuary.Census
        services.Configure<CensusQueryOptions>
            (
                "sanctuary",
                builder.Configuration.GetSection(nameof(CensusQueryOptions))
            )
            .Configure<CensusQueryOptions>
            (
                "sanctuary",
                o =>
                {
                    o.RootEndpoint = "https://census.lithafalcon.cc";
                    o.LanguageCode = CensusLanguage.English;
                    o.Limit = 50;
                }
            );

        services.AddCensusRestServices()
            .AddTransient<ICensusCharacterService, CensusCharacterService>()
            .AddTransient<ICensusItemService, CensusItemService>()
            .AddTransient<ICensusItemCategoryService, CensusItemCategoryService>()
            .AddTransient<ICensusLoadoutService, CensusLoadoutService>()
            .AddTransient<ICensusMapRegionService, CensusMapRegionService>()
            .AddTransient<ICensusOutfitService, CensusOutfitService>()
            .AddTransient<ICensusProfileService, CensusProfileService>()
            .AddTransient<ICensusVehicleService, CensusVehicleService>()
            .AddTransient<ICensusWorldService, CensusWorldService>()
            .AddTransient<ICensusZoneService, CensusZoneService>();

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

        // Register PlanetSide services
        services.AddTransient<IItemCategoryService, ItemCategoryService>();
        services.AddTransient<ILoadoutService, LoadoutService>();
        services.AddTransient<IOutfitService, OutfitService>();

        // Register Scrim services
        services.AddSingleton<IScrimMessageBroadcastService, ScrimMessageBroadcastService>();
        services.AddSingleton<IScrimRulesetManager, ScrimRulesetManager>();

        services.AddSingleton<IScrimTeamsManager, ScrimTeamsManager>();
        services.AddSingleton<IScrimPlayersService, ScrimPlayersService>();

        services.AddSingleton<IStatefulTimer, StatefulTimer>();
        services.AddSingleton<IScrimMatchDataService, ScrimMatchDataService>();
        services.AddSingleton<IScrimMatchEngine, ScrimMatchEngine>();
        services.AddSingleton<IScrimMatchScorer, ScrimMatchScorer>();
        services.AddTransient<IScrimMatchReportDataService, ScrimMatchReportDataService>();

        // Register Ruleset services
        services.AddSingleton<IRulesetDataService, RulesetDataService>();
        services.AddSingleton<IRulesetFileService, RulesetFileService>();

        services.AddSingleton<IConstructedTeamService, ConstructedTeamService>();

        services.AddSingleton<IApplicationDataLoader, ApplicationDataLoader>();

        services.AddHostedService<ApplicationDataLoaderHostedService>();
        services.AddHostedService<StoreRefreshWorker>();

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

    private static void SetupLogger(IConfiguration configuration, IHostEnvironment environment)
    {
        string? seqIngestionEndpoint = configuration["LoggingOptions:SeqIngestionEndpoint"];
        string? seqApiKey = configuration["LoggingOptions:SeqApiKey"];

        LoggerConfiguration loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
            .MinimumLevel.Override("Serilog.AspNetCore.RequestLoggingMiddleware", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console
            (
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            );

        bool useSeq = environment.IsProduction()
            && !string.IsNullOrEmpty(seqIngestionEndpoint)
            && !string.IsNullOrEmpty(seqApiKey);
        if (useSeq)
        {
            Serilog.Core.LoggingLevelSwitch levelSwitch = new();
            loggerConfig.MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Seq(seqIngestionEndpoint!, apiKey: seqApiKey, controlLevelSwitch: levelSwitch);
        }

        Log.Logger = loggerConfig.CreateLogger();
    }
}
