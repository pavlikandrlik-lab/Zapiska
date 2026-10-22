using System.Globalization;

namespace PmTracker.Web.Services.Records;

/// <summary>Záznam v seznamu pro procházení šipkami (spec 2026-10-22 R7, R8).</summary>
public sealed record RecordSequenceEntry(int ZaznamId, string CisloViditelne, string Nazev)
{
    /// <summary>Stejný tvar jako nadpis karty — do nápovědy tlačítek Předchozí / Další.</summary>
    public string Label => $"#{CisloViditelne} - {Nazev}";
}

/// <summary>
/// Spec 2026-10-22 R11: seznam pro procházení z parametru adresy „seznam“ — <c>aktivni</c>
/// (aktivní záznamy projektu) nebo <c>jednani-{id}</c> (záznamy jednání).
/// </summary>
public sealed record RecordSequenceRequest
{
    private const string ActiveValue = "aktivni";
    private const string MeetingPrefix = "jednani-";

    private RecordSequenceRequest(int? jednaniId) => JednaniId = jednaniId;

    /// <summary>Jednání, jehož záznamy se procházejí; null = aktivní záznamy projektu.</summary>
    public int? JednaniId { get; }

    public static RecordSequenceRequest Active { get; } = new((int?)null);

    public static RecordSequenceRequest ForMeeting(int jednaniId) => new(jednaniId);

    /// <summary>Null = parametr chybí nebo je neplatný — stránka bez šipek.</summary>
    public static RecordSequenceRequest? Parse(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, ActiveValue, StringComparison.OrdinalIgnoreCase))
        {
            return Active;
        }

        return normalized.StartsWith(MeetingPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized[MeetingPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var jednaniId)
            && jednaniId > 0
                ? ForMeeting(jednaniId)
                : null;
    }

    public string ToQueryValue()
        => JednaniId is int jednaniId ? MeetingPrefix + jednaniId.ToString(CultureInfo.InvariantCulture) : ActiveValue;
}

/// <summary>Poloha záznamu v seznamu (spec 2026-10-22 R10). Index od 0; kraje bez přetáčení.</summary>
public sealed record RecordSequencePosition(int Index, int Count, RecordSequenceEntry? Previous, RecordSequenceEntry? Next)
{
    /// <summary>Null = záznam v seznamu není (R12) — stránka bez šipek.</summary>
    public static RecordSequencePosition? Locate(IReadOnlyList<RecordSequenceEntry> sequence, int zaznamId)
    {
        for (var index = 0; index < sequence.Count; index++)
        {
            if (sequence[index].ZaznamId != zaznamId)
            {
                continue;
            }

            return new RecordSequencePosition(
                index,
                sequence.Count,
                index > 0 ? sequence[index - 1] : null,
                index < sequence.Count - 1 ? sequence[index + 1] : null);
        }

        return null;
    }
}
