using System.Globalization;

namespace PmTracker.Web.Services.Records;

/// <summary>Jednání projektu, jak ho potřebuje nabídka „Změna stavu platí k“.</summary>
public sealed record StatusChangeMeetingCandidate(int Id, int CisloJednani, DateTime Datum, string? StavKod, bool IsClosed);

/// <summary>Volba nabídky. <see cref="Value"/> je hodnota pole StavPlatiK: prázdná = dnes, jinak id jednání.</summary>
public sealed record StatusChangeEffectiveDateOption(string Value, int? JednaniId, DateTime Datum, string Label);

public sealed record StatusChangeEffectiveDateChoice(
    IReadOnlyList<StatusChangeEffectiveDateOption> Options,
    string DefaultValue,
    DateTime LowerBound);

public sealed record StatusChangeEffectiveDateResolution(StatusChangeEffectiveDateOption? Option, string? Error);

/// <summary>
/// K jakému datu platí změna stavu úkolu — spec 2026-10-13 §4.2 (nabídka), §4.3 (výchozí
/// volba), §4.4 (spodní mez) a §7 (hlášky). Čistá funkce; data načítá StatusChangeEffectiveDateQuery.
/// </summary>
public static class StatusChangeEffectiveDatePolicy
{
    public const string TodayValue = "";

    private const string DraftCode = "DRAFT";
    private const string OpenCode = "OPEN";
    private const string RetryHint = "Vyberte znovu, k čemu změna stavu platí.";

    /// <summary>
    /// Editor otevřený z detailu jednání (plán 2026-10-21): předvybere toto jednání, je-li v nabídce.
    /// Jinak výchozí volba podle §4.3 beze změny — nabídku ani ověření při uložení to nemění.
    /// </summary>
    public static StatusChangeEffectiveDateChoice PreferMeeting(StatusChangeEffectiveDateChoice choice, int meetingId)
    {
        var option = choice.Options.FirstOrDefault(x => x.JednaniId == meetingId);
        return option is null ? choice : choice with { DefaultValue = option.Value };
    }

    public static StatusChangeEffectiveDateChoice Build(
        IReadOnlyList<StatusChangeMeetingCandidate> meetings,
        DateTime today,
        DateTime? lastEffectiveDate,
        DateTime recordFoundedOn)
    {
        var lowerBound = recordFoundedOn.Date;
        if (lastEffectiveDate.HasValue && lastEffectiveDate.Value.Date > lowerBound)
        {
            lowerBound = lastEffectiveDate.Value.Date;
        }

        var offered = new List<(StatusChangeEffectiveDateOption Option, bool IsOpenMeeting)>
        {
            (new StatusChangeEffectiveDateOption(
                TodayValue,
                null,
                today.Date,
                string.Create(CultureInfo.InvariantCulture, $"Dnes ({today:dd.MM.yyyy})")), false)
        };

        foreach (var meeting in meetings.OrderByDescending(x => x.CisloJednani))
        {
            if (meeting.IsClosed)
            {
                break;
            }

            var isDraft = string.Equals(meeting.StavKod, DraftCode, StringComparison.OrdinalIgnoreCase);
            var isOpen = string.Equals(meeting.StavKod, OpenCode, StringComparison.OrdinalIgnoreCase);
            if (!isDraft && !isOpen)
            {
                continue;
            }

            var label = string.Create(
                CultureInfo.InvariantCulture,
                $"Jednání č. {meeting.CisloJednani} – {meeting.Datum:dd.MM.yyyy}{(isDraft ? " (příprava)" : string.Empty)}");
            offered.Add((new StatusChangeEffectiveDateOption(
                meeting.Id.ToString(CultureInfo.InvariantCulture),
                meeting.Id,
                meeting.Datum.Date,
                label), isOpen));
        }

        var allowed = offered.Where(x => x.Option.Datum >= lowerBound).ToList();
        var defaultValue = allowed.Where(x => x.IsOpenMeeting).Select(x => x.Option.Value).FirstOrDefault()
            ?? allowed.Where(x => x.Option.Value == TodayValue).Select(x => x.Option.Value).FirstOrDefault()
            ?? allowed.Select(x => x.Option.Value).FirstOrDefault()
            ?? TodayValue;

        return new StatusChangeEffectiveDateChoice(allowed.Select(x => x.Option).ToList(), defaultValue, lowerBound);
    }

    public static StatusChangeEffectiveDateResolution Resolve(
        StatusChangeEffectiveDateChoice choice,
        IReadOnlyList<StatusChangeMeetingCandidate> meetings,
        string? requestedValue)
    {
        var value = string.IsNullOrWhiteSpace(requestedValue) ? TodayValue : requestedValue.Trim();
        var option = choice.Options.FirstOrDefault(x => x.Value == value);
        if (option is not null)
        {
            return new StatusChangeEffectiveDateResolution(option, null);
        }

        var lowerBound = choice.LowerBound.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        if (choice.Options.Count == 0)
        {
            return Rejected(
                $"Stav teď nejde změnit: změna nemůže platit dříve než {lowerBound} (předchozí změna stavu nebo založení záznamu) a žádné nabízené datum není pozdější.");
        }

        var belowLowerBound = $"Změna stavu nemůže platit dříve než {lowerBound} (předchozí změna stavu nebo založení záznamu).";
        if (value == TodayValue)
        {
            return Rejected(belowLowerBound);
        }

        var meeting = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var meetingId)
            ? meetings.FirstOrDefault(x => x.Id == meetingId)
            : null;
        if (meeting is null)
        {
            return Rejected($"Vybrané jednání neexistuje nebo bylo smazáno. {RetryHint}");
        }

        return meeting.Datum.Date < choice.LowerBound
            ? Rejected(belowLowerBound)
            : Rejected($"Jednání č. {meeting.CisloJednani} teď nejde zvolit — je uzavřené nebo leží za uzavřeným jednáním. {RetryHint}");
    }

    private static StatusChangeEffectiveDateResolution Rejected(string error) => new(null, error);
}
