using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace squittal.ScrimPlanetmans.App.Logging;

public class SqlScriptFileHandler
{
    public static IEnumerable<string> GetAdHocSqlFileNames()
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        string adhocScriptDirectory = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "sql_adhoc"));
        if (!Directory.Exists(adhocScriptDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(adhocScriptDirectory)
            .Where(f => f.EndsWith(".sql"))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f);
    }
}
