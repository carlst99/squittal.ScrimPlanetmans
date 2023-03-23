using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.Logging;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.Services.Interfaces;

namespace squittal.ScrimPlanetmans.App.Services;

public class DatabaseMaintenanceService
{
    private readonly ISqlScriptRunner _adhocScriptRunner;

    private bool _isInitialLoadComplete;

    public List<CensusStoreDataComparisonRow> Comparisons { get; } = new();

    public DatabaseMaintenanceService
    (
        ISqlScriptRunner adhocScriptRunner
    )
    {
        _adhocScriptRunner = adhocScriptRunner;
    }

    public async Task InitializeCounts()
    {
        if (_isInitialLoadComplete)
        {
            return;
        }
        else
        {
            await SetAllCounts();
            _isInitialLoadComplete = true;
        }
    }

    public async Task SetAllCounts()
    {
        var TaskList = new List<Task>();

        foreach (var comparisonRow in Comparisons)
        {
            TaskList.Add(comparisonRow.SetCounts());
        }

        await Task.WhenAll(TaskList);
    }

    public async Task RefreshAllFromCensusAsync(CancellationToken ct)
    {
        var TaskList = new List<Task>();

        foreach (var comparisonRow in Comparisons)
        {
            TaskList.Add(comparisonRow.RefreshStoreFromCensusAsync(ct));
        }

        await Task.WhenAll(TaskList);
    }

    public async Task RefreshAllFromBackupAsync(CancellationToken ct)
    {
        var TaskList = new List<Task>();

        foreach (var comparisonRow in Comparisons)
        {
            TaskList.Add(comparisonRow.RefreshStoreFromBackupAsync(ct));
        }

        await Task.WhenAll(TaskList);
    }

    public IEnumerable<string> GetAdHocSqlFileNames()
    {
        return SqlScriptFileHandler.GetAdHocSqlFileNames();
    }

    public bool TryRunAdHocSqlScript(string fileName, out string info)
    {
        return _adhocScriptRunner.TryRunAdHocSqlScript(fileName, out info);
    }
}
