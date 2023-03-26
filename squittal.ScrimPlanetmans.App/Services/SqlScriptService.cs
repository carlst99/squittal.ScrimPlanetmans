using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Smo;
using squittal.ScrimPlanetmans.App.Abstractions.Services;

namespace squittal.ScrimPlanetmans.App.Services;

public class SqlScriptService : ISqlScriptService
{
    private readonly ILogger<SqlScriptService> _logger;
    private readonly string _sqlDirectory = Path.Combine("Data", "SQL");
    private readonly string _scriptDirectory;
    private readonly string _adhocScriptDirectory;
    private readonly Server _server;

    public SqlScriptService(ILogger<SqlScriptService> logger, IConfiguration configuration)
    {
        _logger = logger;

        string basePath = AppDomain.CurrentDomain.RelativeSearchPath ?? AppDomain.CurrentDomain.BaseDirectory;
        _scriptDirectory = Path.Combine(basePath, _sqlDirectory);

        _adhocScriptDirectory = Path.Combine(basePath, "..", "..", "..", "..", "sql_adhoc");

        string? dbConnectionString = configuration.GetConnectionString("PlanetmansDbContext");
        if (dbConnectionString is null)
            throw new InvalidOperationException("Must set a database connection string by configuration");

        _server = new Server
        {
            ConnectionContext =
            {
                ConnectionString = dbConnectionString
            }
        };
    }

    public void RunSqlScript(string fileName, bool minimalLogging = false)
    {
        string scriptPath = Path.Combine(_scriptDirectory, fileName);
        string scriptText = File.ReadAllText(scriptPath);

        try
        {
            _server.ConnectionContext.ExecuteNonQuery(scriptText);

            if (!minimalLogging)
                _logger.LogInformation("Successfully ran sql script at {Path}", scriptPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running sql script {Path}", scriptPath);
        }
    }

    public bool TryRunAdHocSqlScript(string fileName, out string info, bool minimalLogging = false)
    {
        string scriptPath = Path.Combine(_adhocScriptDirectory, fileName);
        string scriptText = File.ReadAllText(scriptPath);

        try
        {
            _server.ConnectionContext.ExecuteNonQuery(scriptText);
            info = $"Successfully ran sql script at {scriptPath}";

            if (!minimalLogging)
                _logger.LogInformation("Successfully ran sql script at {Path}", scriptPath);

            return true;
        }
        catch (Exception ex)
        {
            info = $"Error running sql script {scriptPath}: {ex}";
            _logger.LogError(ex, "Error running sql script {Path}", scriptPath);

            return false;
        }
    }

    public void RunSqlDirectoryScripts(string directoryName)
    {
        string directoryPath = Path.Combine(_scriptDirectory, directoryName);

        try
        {
            IEnumerable<string> files = Directory.GetFiles(directoryPath)
                .Where(f => f.EndsWith(".sql"))
                .OrderBy(f => f);

            foreach (string file in files)
            {
                _logger.LogDebug("Running sql file {FileName}", file);
                RunSqlScript(file, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running SQL scripts in directory {Name}", directoryName);
        }
    }

    public IEnumerable<string> GetAdHocSqlFileNames()
    {
        if (!Directory.Exists(_adhocScriptDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_adhocScriptDirectory)
            .Where(f => f.EndsWith(".sql"))
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f);
    }
}
