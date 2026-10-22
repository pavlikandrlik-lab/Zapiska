namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-22 R13: text trvání kroku v tabulce harmonogramu. Krok bez plánu má „–“
/// (dřív „0 dnů“, jako by plán byl nulový). Zrcadlo v JS: harmonogram/planDuration.js
/// (formatDuration) — editor text přepočítává při změně data i po ✕.
/// </summary>
public static class ScheduleDurationText
{
    public const string NoPlan = "–";

    public static string Format(int days)
        => $"{days} {(days == 1 ? "den" : days is >= 2 and <= 4 ? "dny" : "dnů")}";

    public static string ForStep(DateTime? planDatum, int trvaniDni)
        => planDatum.HasValue ? Format(trvaniDni) : NoPlan;
}
