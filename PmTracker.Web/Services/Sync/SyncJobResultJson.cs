using System.Text.Json;
using PmTracker.Web.Models.ViewModels.Sync;

namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Review finding Q-4/A-1: sdílený parser LastResultJson pro
/// <see cref="SyncJobAdminHandlerBase{TSettings}"/>. Dříve bylo duplikováno
/// 3× (AD, SD active, SD archive) s mírně odlišnými fallback mapováními.
/// </summary>
public static class SyncJobResultJson
{
    /// <summary>
    /// Parsuje JSON do <see cref="SyncJobResultSummary"/>. Pokud je JSON null/whitespace
    /// nebo malformed → vrací null (karta zobrazí "žádný poslední výsledek").
    /// </summary>
    /// <param name="okCountFields">Pořadí field names, které se mají zkusit pro OkCount
    /// (např. SD używa "ticketsDrilled" před "okCount"). V rámci každého záznamu jde
    /// i camelCase i PascalCase varianta.</param>
    public static SyncJobResultSummary? ParseSummary(string? json, IReadOnlyList<string>? okCountFields = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new SyncJobResultSummary
            {
                StartedAt = TryGetDateTime(root, "startedAt") ?? TryGetDateTime(root, "StartedAt"),
                FinishedAt = TryGetDateTime(root, "finishedAt") ?? TryGetDateTime(root, "FinishedAt"),
                DurationMs = TryGetInt64(root, "durationMs") ?? TryGetInt64(root, "DurationMs"),
                OkCount = ResolveOkCount(root, okCountFields),
                ErrorCount = TryGetInt32(root, "errorCount") ?? TryGetInt32(root, "ErrorCount"),
                ErrorSummary = TryGetFirstErrorReason(root)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ResolveOkCount(JsonElement root, IReadOnlyList<string>? okCountFields)
    {
        // Default fallback chain: okCount / OkCount.
        var fields = okCountFields is { Count: > 0 }
            ? okCountFields
            : new[] { "okCount" };

        foreach (var field in fields)
        {
            var v = TryGetInt32(root, field) ?? TryGetInt32(root, ToPascalCase(field));
            if (v.HasValue) return v;
        }
        return null;
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s) || char.IsUpper(s[0])) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    public static DateTime? TryGetDateTime(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.String && p.TryGetDateTime(out var dt)
            ? dt
            : null;

    public static long? TryGetInt64(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.Number && p.TryGetInt64(out var v)
            ? v
            : null;

    public static int? TryGetInt32(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.Number && p.TryGetInt32(out var v)
            ? v
            : null;

    public static string? TryGetFirstErrorReason(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errs) && !root.TryGetProperty("Errors", out errs))
        {
            return null;
        }
        if (errs.ValueKind != JsonValueKind.Array || errs.GetArrayLength() == 0)
        {
            return null;
        }
        var first = errs[0];
        if (first.TryGetProperty("reason", out var r) || first.TryGetProperty("Reason", out r))
        {
            return r.GetString();
        }
        return null;
    }
}
