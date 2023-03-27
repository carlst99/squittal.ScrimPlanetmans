using System.IO;

namespace squittal.ScrimPlanetmans.App.Models;

public class LooseFileOptions
{
    private readonly string DefaultAdhocDirectory = Path.Combine("..", "..", "..", "..", "sql_adhoc");
    private readonly string DefaultRulesetsDirectory = Path.Combine("..", "..", "..", "..", "rulesets");

    private readonly string? _adhoc;
    private readonly string? _rulesets;

    public string AdHocSqlScriptsDirectory
    {
        get => string.IsNullOrEmpty(_adhoc) ? DefaultAdhocDirectory : _adhoc;
        init => _adhoc = value;
    }

    public string RulesetsDirectory
    {
        get => string.IsNullOrEmpty(_rulesets) ? DefaultRulesetsDirectory : _rulesets;
        init => _rulesets = value;
    }
}
