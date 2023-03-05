namespace squittal.ScrimPlanetmans.App.Services.Interfaces;

public interface ISqlScriptRunner
{
    void RunSqlDirectoryScripts(string directoryName);
    void RunSqlScript(string fileName, bool minimalLogging = false);
    bool TryRunAdHocSqlScript(string fileName, out string info, bool minimalLogging = false);
}
