using System.Collections.Generic;

namespace squittal.ScrimPlanetmans.App.Abstractions.Services;

public interface ISqlScriptService
{
    void RunSqlDirectoryScripts(string directoryName);
    void RunSqlScript(string fileName, bool minimalLogging = false);
    bool TryRunAdHocSqlScript(string fileName, out string info, bool minimalLogging = false);
    IEnumerable<string> GetAdHocSqlFileNames();
}
