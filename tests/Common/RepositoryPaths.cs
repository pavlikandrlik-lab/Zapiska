using System.Text.RegularExpressions;

namespace PmTracker.Tests.Common;

public static class RepositoryPaths
{
    public static string Root { get; } = ResolveRepositoryRoot();

    public static string WebProjectPath => Path.Combine(Root, "PmTracker.Web", "PmTracker.Web.csproj");

    public static string WebProjectDirectory => Path.GetDirectoryName(WebProjectPath)!;

    public static IReadOnlyList<string> GetBootstrapScripts(bool includeSeed)
    {
        var scriptsFromDocs = LoadScriptsFromBootstrapDocumentation();
        if (scriptsFromDocs.Count > 0)
        {
            var resolved = scriptsFromDocs
                .Select(ResolveRequiredScriptPath)
                .ToList();

            if (includeSeed)
            {
                var seedPath = Path.Combine(Root, "db_seed_dev_admin.sql");
                if (File.Exists(seedPath))
                {
                    resolved.Add(seedPath);
                }
            }

            return resolved;
        }

        var fallback = new List<string>
        {
            ResolveRequiredScriptPath("PMTracker_insert_sql")
        };

        if (includeSeed)
        {
            var seedPath = Path.Combine(Root, "db_seed_dev_admin.sql");
            if (File.Exists(seedPath))
            {
                fallback.Add(seedPath);
            }
        }

        return fallback;
    }

    private static IReadOnlyList<string> LoadScriptsFromBootstrapDocumentation()
    {
        var docPath = Path.Combine(Root, "docs", "technical", "06-database-bootstrap-migrations.md");
        if (!File.Exists(docPath))
        {
            return Array.Empty<string>();
        }

        var lines = File.ReadAllLines(docPath);
        var scripts = new List<string>();
        var regex = new Regex("^\\s*\\d+\\.\\s+`([^`]+)`", RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var match = regex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var scriptName = match.Groups[1].Value.Trim();
            if (scriptName.Contains("seed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scripts.Add(scriptName);
        }

        if (scripts.Count == 0)
        {
            return Array.Empty<string>();
        }

        return scripts;
    }

    private static string ResolveRequiredScriptPath(string scriptName)
    {
        var direct = Path.Combine(Root, scriptName);
        if (File.Exists(direct))
        {
            return direct;
        }

        var fallback = Directory
            .GetFiles(Root, "*.sql", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), scriptName, StringComparison.OrdinalIgnoreCase));

        if (fallback is not null)
        {
            return fallback;
        }

        throw new FileNotFoundException($"Nepodařilo se najít SQL skript '{scriptName}'.");
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var slnPath = Path.Combine(directory.FullName, "PmTracker.sln");
            if (File.Exists(slnPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
    }
}
