using System.Text;
using Microsoft.Data.SqlClient;

namespace PmTracker.Tests.Common;

public static class SqlScriptRunner
{
    public static async Task ExecuteScriptsAsync(string connectionString, IReadOnlyList<string> scriptPaths, CancellationToken cancellationToken = default)
    {
        if (scriptPaths.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var scriptPath in scriptPaths)
        {
            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            foreach (var batch in SplitBatches(sql))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    public static IEnumerable<string> SplitBatches(string script)
    {
        using var reader = new StringReader(script);
        var builder = new StringBuilder();

        while (reader.ReadLine() is { } line)
        {
            if (IsGoBatchDelimiter(line))
            {
                var batch = builder.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    yield return batch;
                }

                builder.Clear();
                continue;
            }

            builder.AppendLine(line);
        }

        var remaining = builder.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
        {
            yield return remaining;
        }
    }

    private static bool IsGoBatchDelimiter(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 2)
        {
            return false;
        }

        if (!trimmed.StartsWith("GO", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trimmed.Length == 2)
        {
            return true;
        }

        return char.IsWhiteSpace(trimmed[2]) || trimmed[2] == '-';
    }
}
