using System;
using System.IO;

namespace squittal.ScrimPlanetmans.App.Models;

public class LooseFileOptions
{
    public const string OptionsName = "LooseFileOptions";

    private readonly string? _adhoc;
    private readonly string? _rulesets;

    public string AdHocSqlScriptsDirectory
    {
        get => string.IsNullOrEmpty(_adhoc) ? GetDefaultPath("sql_adhoc") : _adhoc;
        init => _adhoc = value;
    }

    public string RulesetsDirectory
    {
        get => string.IsNullOrEmpty(_rulesets) ? GetDefaultPath("rulesets") : _rulesets;
        init => _rulesets = value;
    }

    private static string GetDefaultPath(string directoryName)
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(basePath, "..", "..", "..", "..", directoryName);
    }
}
