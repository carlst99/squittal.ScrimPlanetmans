using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Services.Rulesets;

/// <inheritdoc />
public class RulesetFileService : IRulesetFileService
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new() { WriteIndented = true };

    private readonly ILogger<RulesetFileService> _logger;

    public RulesetFileService(ILogger<RulesetFileService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> WriteToJsonFileAsync(string fileName, JsonRuleset ruleset, CancellationToken ct = default)
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        fileName = Path.GetFileNameWithoutExtension(fileName) + ".json";
        string path = Path.Combine(basePath, "..", "..", "..", "..", "rulesets", fileName);

        try
        {
            await using FileStream fileStream = File.Create(path);
            await JsonSerializer.SerializeAsync(fileStream, ruleset, JSON_OPTIONS, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write ruleset to a JSON file: {Path}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<JsonRuleset?> ReadFromJsonFileAsync(string fileName, CancellationToken ct = default)
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        fileName = Path.GetFileNameWithoutExtension(fileName) + ".json";
        string path = Path.Combine(basePath, "..", "..", "..", "..", "rulesets", fileName);

        try
        {
            await using FileStream fileStream = File.OpenRead(path);
            JsonRuleset? ruleset = await JsonSerializer.DeserializeAsync<JsonRuleset>(fileStream, cancellationToken: ct);

            return ruleset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read a ruleset from a JSON file at {Path}", path);
            return null;
        }
    }

    /// <inheritdoc />
    public IEnumerable<string> GetJsonRulesetFileNames()
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        string rulesetsDirectory = Path.Combine(basePath, "..", "..", "..", "..", "rulesets");
        if (!Directory.Exists(rulesetsDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(rulesetsDirectory)
            .Where(f => f.EndsWith(".json"))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f);
    }
}
