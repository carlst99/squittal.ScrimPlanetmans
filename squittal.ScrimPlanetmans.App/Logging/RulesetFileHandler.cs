using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using squittal.ScrimPlanetmans.App.ScrimMatch.Ruleset.Models;

namespace squittal.ScrimPlanetmans.App.Logging;

public class RulesetFileHandler
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new() { WriteIndented = true };

    public static async Task<bool> WriteToJsonFile(string fileName, JsonRuleset ruleset)
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        string path = Path.Combine(basePath, "..", "..", "..", "..", "rulesets", Path.ChangeExtension(fileName, "json"));

        try
        {
            await using FileStream fileStream = File.Create(path);
            await JsonSerializer.SerializeAsync(fileStream, ruleset, JSON_OPTIONS);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<JsonRuleset?> ReadFromJsonFile(string fileName)
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        string path = Path.Combine(basePath, "..", "..", "..", "..", "rulesets", Path.ChangeExtension(fileName, "json"));

        try
        {
            await using FileStream fileStream = File.OpenRead(path);
            JsonRuleset? ruleset = await JsonSerializer.DeserializeAsync<JsonRuleset>(fileStream);

            return ruleset;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex}");
            return null;
        }
    }

    public static IEnumerable<string> GetJsonRulesetFileNames()
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
