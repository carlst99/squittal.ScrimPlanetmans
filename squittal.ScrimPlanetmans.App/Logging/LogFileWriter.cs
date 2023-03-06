using System;
using System.IO;

namespace squittal.ScrimPlanetmans.App.Logging;

public sealed class LogFileWriter : IDisposable
{
    private readonly StreamWriter _logStream;

    public LogFileWriter(string fileName)
    {
        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        string logPath = Path.Combine(basePath, "..", "..", "..", "..", "match_logs", fileName);
        FileStream fileStream = new(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        _logStream = new StreamWriter(fileStream);
    }

    public void Write(string message)
    {
        _logStream.WriteLine(message);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _logStream.Dispose();
    }
}
