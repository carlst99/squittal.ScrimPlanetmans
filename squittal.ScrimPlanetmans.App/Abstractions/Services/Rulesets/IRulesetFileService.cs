using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;

public interface IRulesetFileService
{
    Task<bool> WriteToJsonFileAsync(string fileName, JsonRuleset ruleset, CancellationToken ct = default);
    Task<JsonRuleset?> ReadFromJsonFileAsync(string fileName, CancellationToken ct = default);
    IEnumerable<string> GetJsonRulesetFileNames();
}
