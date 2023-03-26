using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;
using squittal.ScrimPlanetmans.App.Data;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Forms;
using squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Services.ScrimMatch;

public class ScrimMatchReportDataService : IScrimMatchReportDataService
{
    private const int SCRIM_MATCH_BROWSER_PAGE_SIZE = 15;

    private readonly IDbContextFactory<PlanetmansDbContext> _dbContextFactory;
    private readonly ILogger<ScrimMatchReportDataService> _logger;

    public ScrimMatchReportDataService(IDbContextFactory<PlanetmansDbContext> dbContextFactory, ILogger<ScrimMatchReportDataService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<PaginatedList<ScrimMatchInfo>> GetHistoricalScrimMatchesListAsync
    (
        int? pageIndex,
        ScrimMatchReportBrowserSearchFilter searchFilter,
        CancellationToken ct
    )
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            IQueryable<ScrimMatchInfo> scrimMatchesQuery = dbContext.ScrimMatchInfo
                .Where(GetHistoricalScrimMatchBrowserWhereExpression(searchFilter))
                .AsQueryable();

            PaginatedList<ScrimMatchInfo> paginatedList = await PaginatedList<ScrimMatchInfo>.CreateAsync
            (
                scrimMatchesQuery.OrderByDescending(m => m.StartTime),
                pageIndex ?? 1,
                SCRIM_MATCH_BROWSER_PAGE_SIZE,
                ct
            );

            foreach (ScrimMatchInfo match in paginatedList.Contents)
                match.SetTeamAliases();

            return paginatedList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get historical scrim matches");
            return new PaginatedList<ScrimMatchInfo>(Array.Empty<ScrimMatchInfo>(), 0, 1, 0);
        }
    }

    private static Expression<Func<ScrimMatchInfo, bool>> GetHistoricalScrimMatchBrowserWhereExpression
    (
        ScrimMatchReportBrowserSearchFilter searchFilter
    )
    {
        bool isDefaultFilter = searchFilter.IsDefaultFilter;

        Expression<Func<ScrimMatchInfo, bool>>? whereExpression;

        if (isDefaultFilter)
        {
            Expression<Func<ScrimMatchInfo, bool>> roundExpression = m => m.RoundCount >= searchFilter.MinimumRoundCount;

            DateTime twoHoursAgo = DateTime.UtcNow - TimeSpan.FromHours(2);
            Expression<Func<ScrimMatchInfo, bool>> recentMatchExpression = m => m.StartTime >= twoHoursAgo;

            roundExpression = roundExpression.Or(recentMatchExpression);
            whereExpression = roundExpression;
        }
        else
        {
            Expression<Func<ScrimMatchInfo, bool>> roundExpression = m => m.RoundCount >= searchFilter.MinimumRoundCount;
            whereExpression = roundExpression;
        }

        if (searchFilter.SearchStartDate != null)
        {
            Expression<Func<ScrimMatchInfo, bool>> startDateExpression = m => m.StartTime >= searchFilter.SearchStartDate;

            whereExpression = whereExpression.And(startDateExpression);
        }

        if (searchFilter.SearchEndDate != null)
        {
            Expression<Func<ScrimMatchInfo, bool>> endDateExpression = m => m.StartTime <= searchFilter.SearchEndDate;

            whereExpression = whereExpression.And(endDateExpression);
        }

        if (searchFilter.RulesetId != 0)
        {
            Expression<Func<ScrimMatchInfo, bool>> rulesetExpression = m => m.RulesetId == searchFilter.RulesetId;

            whereExpression = whereExpression.And(rulesetExpression);
        }

        if (searchFilter.FacilityId != -1)
        {
            Expression<Func<ScrimMatchInfo, bool>> facilityExpression;

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
            Expression<Func<ScrimMatchInfo, bool>> worldExpression = m => m.WorldId == searchFilter.WorldId;

            whereExpression = whereExpression.And(worldExpression);
        }

        if (searchFilter.SearchTermsList.Any() || searchFilter.AliasSearchTermsList.Any())
        {
            Expression<Func<ScrimMatchInfo, bool>>? searchTermsExpression = null;
            Expression<Func<ScrimMatchInfo, bool>>? aliasTermsExpression = null;

            foreach (string term in searchFilter.SearchTermsList)
            {
                Expression<Func<ScrimMatchInfo, bool>> exp = m => m.Title.Contains(term); // DbFunctionsExtensions.Like(EF.Functions, m.Title, "%" + term + "%");
                searchTermsExpression = searchTermsExpression == null ? exp : searchTermsExpression.Or(exp);
            }


            foreach (string term in searchFilter.AliasSearchTermsList)
            {
                Expression<Func<ScrimMatchInfo, bool>> exp = m => m.ScrimMatchId.Contains(term); // DbFunctionsExtensions.Like(EF.Functions, m.ScrimMatchId, "%" + term + "%");
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

    public async Task<ScrimMatchInfo?> GetHistoricalScrimMatchInfoAsync(string scrimMatchId, CancellationToken ct)
    {
        await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        ScrimMatchInfo? scrimMatchInfo = await dbContext.ScrimMatchInfo
            .FirstOrDefaultAsync(m => m.ScrimMatchId == scrimMatchId, ct);

        scrimMatchInfo?.SetTeamAliases();

        return scrimMatchInfo;
    }

    public async Task<IEnumerable<uint>> GetScrimMatchBrowserFacilityIdsListAsync(CancellationToken ct)
    {
        await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        return await dbContext.ScrimMatchRoundConfigurations
            .Where(m => m.FacilityId != null)
            .Distinct()
            .Select(m => m.FacilityId!.Value)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Ruleset>> GetScrimMatchBrowseRulesetIdsListAsync(CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            List<int> distinctRulesetIds = await dbContext.ScrimMatchInfo
                .Select(m => m.RulesetId)
                .Distinct()
                .ToListAsync(ct);

            if (!distinctRulesetIds.Any())
                return Array.Empty<Ruleset>();

            return await dbContext.Rulesets
                .Where(r => distinctRulesetIds.Contains(r.Id))
                .ToListAsync(ct);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all scrim match ruleset IDs");
            return Array.Empty<Ruleset>();
        }
    }

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerStats>>  GetHistoricalScrimMatchInfantryPlayerStatsAsync(string scrimMatchId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryPlayerStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.NameDisplay)
                .ToListAsync(ct);

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

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerRoundStats>> GetHistoricalScrimMatchInfantryPlayerRoundStatsAsync(string scrimMatchId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryPlayerRoundStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.NameDisplay)
                .ThenBy(e => e.ScrimMatchRound)
                .ToListAsync(ct);

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

    public async Task<IEnumerable<ScrimMatchReportInfantryTeamStats>> GetHistoricalScrimMatchInfantryTeamStatsAsync(string scrimMatchId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryTeamStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.TeamOrdinal)
                .ToListAsync(ct);
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

    public async Task<IEnumerable<ScrimMatchReportInfantryTeamRoundStats>> GetHistoricalScrimMatchInfantryTeamRoundStatsAsync(string scrimMatchId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryTeamRoundStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderBy(e => e.TeamOrdinal)
                .ThenBy(e => e.ScrimMatchRound)
                .ToListAsync(ct);
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

    public async Task<IEnumerable<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryDeathsAsync(string scrimMatchId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryDeaths
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync(ct);
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

    public async Task<IEnumerable<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryPlayerDeathsAsync(string scrimMatchId, string characterId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryDeaths
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId
                    && ( e.AttackerCharacterId == characterId
                        || e.VictimCharacterId == characterId ) )
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync(ct);
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

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerHeadToHeadStats>> GetHistoricalScrimMatchInfantryPlayerHeadToHeadStatsAsync(string scrimMatchId, string characterId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryPlayerHeadToHeadStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId
                    && e.PlayerCharacterId == characterId)
                .OrderByDescending(e => e.PlayerTeamOrdinal != e.OpponentTeamOrdinal)
                .ThenBy(e => e.OpponentNameDisplay)
                .ToListAsync(ct);
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

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerClassEventCounts>> GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync(string scrimMatchId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryPlayerClassEventCounts
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId)
                .OrderByDescending(e => e.TeamOrdinal)
                .ThenBy(e => e.NameDisplay)
                .ToListAsync(ct);
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

    public async Task<IEnumerable<ScrimMatchReportInfantryPlayerWeaponStats>> GetHistoricalScrimMatchInfantryPlayerWeaponStatsAsync(string scrimMatchId, string characterId, CancellationToken ct)
    {
        try
        {
            await using PlanetmansDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

            return await dbContext.ScrimMatchReportInfantryPlayerWeaponStats
                .AsNoTracking()
                .Where(e => e.ScrimMatchId == scrimMatchId
                    && e.CharacterId == characterId)
                .OrderByDescending(e => e.Kills)
                .ThenByDescending(e => e.Deaths)
                .ThenBy(e => e.WeaponId)
                .ToListAsync(ct);
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
    //        using var factory = _dbContextFactory.GetFactory();
    //        var dbContext = factory.GetDbContext();

    //        var scrimMatchesQuery = dbContext.ScrimMatchReportInfantryDeaths.Where(d => d.ScrimMatchId == scrimMatchId).AsQueryable();

    //        var paginatedList = await PaginatedList<ScrimMatchReportInfantryDeath>.CreateAsync(scrimMatchesQuery.AsNoTracking().OrderByDescending(d => d.Timestamp), pageIndex ?? 1, SCRIM_MATCH_BROWSER_PAGE_SIZE);

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
