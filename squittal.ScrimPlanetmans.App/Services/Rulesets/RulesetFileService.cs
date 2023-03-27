using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using squittal.ScrimPlanetmans.App.Abstractions.Services.Rulesets;
using squittal.ScrimPlanetmans.App.Models;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Services.Rulesets;

/// <inheritdoc />
public class RulesetFileService : IRulesetFileService
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new() { WriteIndented = true };

    private readonly ILogger<RulesetFileService> _logger;
    private readonly string _rulesetsDirectory;

    public RulesetFileService(ILogger<RulesetFileService> logger, IOptions<LooseFileOptions> looseFileOptions)
    {
        _logger = logger;
        _rulesetsDirectory = looseFileOptions.Value.RulesetsDirectory;
    }

    /// <inheritdoc />
    public async Task<bool> WriteToJsonFileAsync(string fileName, JsonRuleset ruleset, CancellationToken ct = default)
    {
        fileName = Path.GetFileNameWithoutExtension(fileName) + ".json";
        string path = Path.Combine(_rulesetsDirectory, fileName);

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
        fileName = Path.GetFileNameWithoutExtension(fileName) + ".json";
        string path = Path.Combine(_rulesetsDirectory, fileName);

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
        if (!Directory.Exists(_rulesetsDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_rulesetsDirectory)
            .Where(f => f.EndsWith(".json"))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f);
    }
}
