using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Data.Interfaces;
using squittal.ScrimPlanetmans.App.Data.Models;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Forms;
using squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;
using squittal.ScrimPlanetmans.App.Services.ScrimMatchReports.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatchReports;

public class ScrimMatchReportDataService : IScrimMatchReportDataService
{
    private readonly IDbContextHelper _dbContextHelper;
    private readonly ILogger<ScrimMatchReportDataService> _logger;

    private readonly int _scrimMatchBrowserPageSize = 15;

    public ScrimMatchReportDataService(IDbContextHelper dbContextHelper, ILogger<ScrimMatchReportDataService> logger)
    {
        _dbContextHelper = dbContextHelper;
        _logger = logger;
    }

    public async Task<PaginatedList<ScrimMatchInfo>> GetHistoricalScrimMatchesListAsync
    (
        int? pageIndex,
        ScrimMatchReportBrowserSearchFilter searchFilter,
        CancellationToken cancellationToken
    )
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            pageIndex ??= 1;

            List<ScrimMatchRoundConfiguration> roundConfigs = await dbContext.ScrimMatchRoundConfigurations
                .Where(GetHistoricalScrimMatchBrowserWhereExpression(searchFilter))
                .Include(x => x.ScrimMatch)
                .GroupBy(x => x.ScrimMatchId)
                .Select(x => x.MaxBy(y => y.ScrimMatchRound))
                .Where(x => x != null)
                .Skip((pageIndex.Value - 1) * _scrimMatchBrowserPageSize)
                .Take(_scrimMatchBrowserPageSize)
                .Cast<ScrimMatchRoundConfiguration>()
                .ToListAsync(cancellationToken);

            return new PaginatedList<ScrimMatchInfo>
            (
                roundConfigs.Select(x => new ScrimMatchInfo(x.ScrimMatch, x)),
                pageIndex.Value,
                _scrimMatchBrowserPageSize
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get historical scrim matches");
            return new PaginatedList<ScrimMatchInfo>(Array.Empty<ScrimMatchInfo>(), 0, 1, 0);
        }
    }

    private static Expression<Func<ScrimMatchRoundConfiguration, bool>> GetHistoricalScrimMatchBrowserWhereExpression
    (
        ScrimMatchReportBrowserSearchFilter searchFilter
    )
    {
        bool isDefaultFilter = searchFilter.IsDefaultFilter;

        Expression<Func<ScrimMatchRoundConfiguration, bool>>? whereExpression;

        if (isDefaultFilter)
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> roundExpression = m => m.ScrimMatchRound >= searchFilter.MinimumRoundCount;

            DateTime twoHoursAgo = DateTime.UtcNow - TimeSpan.FromHours(2);
            Expression<Func<ScrimMatchRoundConfiguration, bool>> recentMatchExpression = m => m.ScrimMatch.StartTime >= twoHoursAgo;

            roundExpression = roundExpression.Or(recentMatchExpression);
            whereExpression = roundExpression;
        }
        else
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> roundExpression = m => m.ScrimMatchRound >= searchFilter.MinimumRoundCount;
            whereExpression = roundExpression;
        }

        if (searchFilter.SearchStartDate != null)
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> startDateExpression = m => m.ScrimMatch.StartTime >= searchFilter.SearchStartDate;

            whereExpression = whereExpression.And(startDateExpression);
        }

        if (searchFilter.SearchEndDate != null)
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> endDateExpression = m => m.ScrimMatch.StartTime <= searchFilter.SearchEndDate;

            whereExpression = whereExpression.And(endDateExpression);
        }

        if (searchFilter.RulesetId != 0)
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> rulesetExpression = m => m.ScrimMatch.RulesetId == searchFilter.RulesetId;

            whereExpression = whereExpression.And(rulesetExpression);
        }

        if (searchFilter.FacilityId != -1)
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> facilityExpression;

            if (searchFilter.FacilityId == 0)
            {
                facilityExpression = m => m.FacilityId != null;
            }
            else
            {
                facilityExpression = m => m.FacilityId == searchFilter.FacilityId;
            }

            whereExpression = whereExpression.And(facilityExpression);
        }

        if (searchFilter.WorldId != 0)
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>> worldExpression = m => m.WorldId == searchFilter.WorldId;

            whereExpression = whereExpression.And(worldExpression);
        }

        if (searchFilter.SearchTermsList.Any() || searchFilter.AliasSearchTermsList.Any())
        {
            Expression<Func<ScrimMatchRoundConfiguration, bool>>? searchTermsExpression = null;
            Expression<Func<ScrimMatchRoundConfiguration, bool>>? aliasTermsExpression = null;

            foreach (string term in searchFilter.SearchTermsList)
            {
                Expression<Func<ScrimMatchRoundConfiguration, bool>> exp = m => m.Title.Contains(term); // DbFunctionsExtensions.Like(EF.Functions, m.Title, "%" + term + "%");
                searchTermsExpression = searchTermsExpression == null ? exp : searchTermsExpression.Or(exp);
            }


            foreach (string term in searchFilter.AliasSearchTermsList)
            {
                Expression<Func<ScrimMatchRoundConfiguration, bool>> exp = m => m.ScrimMatchId.Contains(term); // DbFunctionsExtensions.Like(EF.Functions, m.ScrimMatchId, "%" + term + "%");
                aliasTermsExpression = aliasTermsExpression == null ? exp : aliasTermsExpression.And(exp);
            }

            if (aliasTermsExpression is not null)
            {
                searchTermsExpression = searchTermsExpression == null
                    ? aliasTermsExpression
                    : searchTermsExpression.Or(aliasTermsExpression);
            }

            if (searchTermsExpression is not null)
                whereExpression = whereExpression.And(searchTermsExpression);
        }

        return whereExpression;
    }

    public Task<ScrimMatchInfo?> GetHistoricalScrimMatchInfoAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        ScrimMatchRoundConfiguration? roundConfig = dbContext.ScrimMatchRoundConfigurations
            .Where(rc => rc.ScrimMatchId == scrimMatchId)
            .Include(x => x.ScrimMatch)
            .MaxBy(rc => rc.ScrimMatchRound);

        return roundConfig is null
            ? Task.FromResult<ScrimMatchInfo?>(null)
            : Task.FromResult<ScrimMatchInfo?>(new ScrimMatchInfo(roundConfig.ScrimMatch, roundConfig));
    }

    public async Task<IEnumerable<uint>> GetScrimMatchBrowserFacilityIdsListAsync(CancellationToken cancellationToken)
    {
        using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
        PlanetmansDbContext dbContext = factory.GetDbContext();

        return await dbContext.ScrimMatchRoundConfigurations
            .Where(m => m.FacilityId != null)
            .Distinct()
            .Select(m => m.FacilityId!.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Ruleset>> GetScrimMatchBrowseRulesetIdsListAsync(CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            List<int> distinctRulesetIds = await dbContext.ScrimMatchRoundConfigurations
                .Include(m => m.ScrimMatch)
                .Select(m => m.ScrimMatch.RulesetId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (!distinctRulesetIds.Any())
                return Array.Empty<Ruleset>();

            return await dbContext.Rulesets
                .Where(r => distinctRulesetIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all scrim match ruleset IDs");
            return Array.Empty<Ruleset>();
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerStats>>  GetHistoricalScrimMatchInfantryPlayerStatsAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryPlayerStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.NameDisplay)
                .ToListAsync(cancellationToken);

        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryPlayerStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryPlayerStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerRoundStats>> GetHistoricalScrimMatchInfantryPlayerRoundStatsAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryPlayerRoundStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.NameDisplay)
                .ThenBy(e => e.ScrimMatchRound)
                .ToListAsync(cancellationToken);

        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryPlayerRoundStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryPlayerRoundStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryTeamStats>> GetHistoricalScrimMatchInfantryTeamStatsAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryTeamStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.TeamOrdinal)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryTeamStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryTeamStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryTeamRoundStats>> GetHistoricalScrimMatchInfantryTeamRoundStatsAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryTeamRoundStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.TeamOrdinal)
                .ThenBy(e => e.ScrimMatchRound)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryTeamStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryTeamStatsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryDeathsAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryDeaths
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryDeathsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryDeathsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryPlayerDeathsAsync(string scrimMatchId, string characterId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryDeaths
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId
                    && ( e.AttackerCharacterId == characterId
                        || e.VictimCharacterId == characterId ) )
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryDeathsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryDeathsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerHeadToHeadStats>> GetHistoricalScrimMatchInfantryPlayerHeadToHeadStatsAsync(string scrimMatchId, string characterId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryPlayerHeadToHeadStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId
                    && e.PlayerCharacterId == characterId)
                .OrderByDescending(e => e.PlayerTeamOrdinal != e.OpponentTeamOrdinal)
                .ThenBy(e => e.OpponentNameDisplay)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryPlayerHeadToHeadStatsAsync scrimMatchId {scrimMatchId} characterId {characterId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryPlayerHeadToHeadStatsAsync scrimMatchId {scrimMatchId} characterId {characterId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerClassEventCounts>> GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync(string scrimMatchId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryPlayerClassEventCounts
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderByDescending(e => e.TeamOrdinal)
                .ThenBy(e => e.NameDisplay)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerWeaponStats>> GetHistoricalScrimMatchInfantryPlayerWeaponStatsAsync(string scrimMatchId, string characterId, CancellationToken cancellationToken)
    {
        try
        {
            using DbContextHelper.DbContextFactory factory = _dbContextHelper.GetFactory();
            PlanetmansDbContext dbContext = factory.GetDbContext();

            return await dbContext.ScrimMatchReportInfantryPlayerWeaponStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId
                    && e.CharacterId == characterId)
                .OrderByDescending(e => e.Kills)
                .ThenByDescending(e => e.Deaths)
                .ThenBy(e => e.WeaponId)
                .ToListAsync(cancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation($"Task Request cancelled: GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"Request cancelled: GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync scrimMatchId {scrimMatchId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{ex}");

            return null;
        }
    }

    //public async Task<PaginatedList<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryPlayerDeathsAsync(string scrimMatchId, int? pageIndex)
    //{
    //    try
    //    {
    //        using var factory = _dbContextHelper.GetFactory();
    //        var dbContext = factory.GetDbContext();

    //        var scrimMatchesQuery = dbContext.ScrimMatchReportInfantryDeaths.Where(d => d.ScrimMatchId == scrimMatchId).AsQueryable();

    //        var paginatedList = await PaginatedList<ScrimMatchReportInfantryDeath>.CreateAsync(scrimMatchesQuery.AsNoTracking().OrderByDescending(d => d.Timestamp), pageIndex ?? 1, _scrimMatchBrowserPageSize);

    //        if (paginatedList == null)
    //        {
    //            return null;
    //        }

    //        return paginatedList;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError($"{ex}");

    //        return null;
    //    }
    //}
}
