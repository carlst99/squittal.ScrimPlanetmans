using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Models.Forms;
using squittal.ScrimPlanetmans.App.Models.ScrimMatchReports;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.ScrimMatch;

public interface IScrimMatchReportDataService
{
    Task<PaginatedList<ScrimMatchInfo>> GetHistoricalScrimMatchesListAsync
    (
        int? pageIndex,
        ScrimMatchReportBrowserSearchFilter searchFilter,
        CancellationToken ct
    );

    //Task<PaginatedList<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryPlayerDeathsAsync(string scrimMatchId, int? pageIndex);
    Task<IEnumerable<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryDeathsAsync(string scrimMatchId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryPlayerClassEventCounts>> GetHistoricalScrimMatchInfantryPlayeClassEventCountsAsync(string scrimMatchId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryDeath>> GetHistoricalScrimMatchInfantryPlayerDeathsAsync(string scrimMatchId, string characterId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryPlayerHeadToHeadStats>> GetHistoricalScrimMatchInfantryPlayerHeadToHeadStatsAsync(string scrimMatchId, string characterId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryPlayerRoundStats>> GetHistoricalScrimMatchInfantryPlayerRoundStatsAsync(string scrimMatchId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryPlayerStats>> GetHistoricalScrimMatchInfantryPlayerStatsAsync(string scrimMatchId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryPlayerWeaponStats>> GetHistoricalScrimMatchInfantryPlayerWeaponStatsAsync(string scrimMatchId, string characterId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryTeamRoundStats>> GetHistoricalScrimMatchInfantryTeamRoundStatsAsync(string scrimMatchId, CancellationToken ct);
    Task<IEnumerable<ScrimMatchReportInfantryTeamStats>> GetHistoricalScrimMatchInfantryTeamStatsAsync(string scrimMatchId, CancellationToken ct);
    Task<ScrimMatchInfo?> GetHistoricalScrimMatchInfoAsync(string scrimMatchId, CancellationToken ct);
    Task<IEnumerable<uint>> GetScrimMatchBrowserFacilityIdsListAsync(CancellationToken ct);
    Task<IEnumerable<Ruleset>> GetScrimMatchBrowseRulesetIdsListAsync(CancellationToken ct);
}
