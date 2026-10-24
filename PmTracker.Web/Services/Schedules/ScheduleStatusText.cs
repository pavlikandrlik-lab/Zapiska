using System.Globalization;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-24: texty souhrnu harmonogramu (stav, plán dokončení, trvání) a odchylky kroku
/// v Rozpadu. Jediné místo, kde se skládají — _ScheduleBlock.cshtml je jen volá. Zrcadlo v JS:
/// schedule/scheduleStatus.js (editor je přepočítává živě); parita je zamčená sdílenými případy
/// tests/js/schedule/__fixtures__/status-cases.json (ScheduleStatusGoldenVectorTests + scheduleStatus.test.js).
/// </summary>
public static class ScheduleStatusText
{
    public const string Dokonceno = "Dokončeno";
    public const string PlanNeniZadan = "není zadán";
    public const string KrokBezPlanu = "bez plánu";

    /// <summary>R4: „1 den“, „2–4 dny“, jinak „N dní“.</summary>
    public static string Dni(int pocet)
        => $"{pocet} {(pocet == 1 ? "den" : pocet is >= 2 and <= 4 ? "dny" : "dní")}";

    /// <summary>R3: sjednocený stav. <paramref name="prekroceniDni"/> = dnes − plán (resp. termín), znaménkově.</summary>
    public static string State(HarmonogramStavVztah vztah, string? krokNazev, int prekroceniDni)
    {
        if (vztah == HarmonogramStavVztah.Dokonceno)
        {
            return Dokonceno;
        }

        var krok = $"Aktuální krok: {krokNazev}";
        if (vztah == HarmonogramStavVztah.CelkovyTermin)
        {
            var termin = prekroceniDni > 0 ? $"celkový termín: skluz +{Dni(prekroceniDni)}"
                : prekroceniDni < 0 ? $"celkový termín: {Zbyva(-prekroceniDni)}"
                : "celkový termín je dnes";
            return $"{krok} · plán není zadán · {termin}";
        }

        return prekroceniDni > 0 ? $"{krok} · skluz +{Dni(prekroceniDni)}"
            : prekroceniDni < 0 ? $"{krok} · v předstihu {Dni(-prekroceniDni)}"
            : $"{krok} · dle plánu";
    }

    /// <summary>R6: „Plán dokončení“ / „Plán:“ — datum, nebo „není zadán“, když žádný krok nemá plán.</summary>
    public static string PlanCompletion(DateTime? planoveDokonceni)
        => planoveDokonceni?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? PlanNeniZadan;

    /// <summary>R6: celkové trvání v editoru — „–“ bez plánu, jinak stejné tvary jako trvání kroku.</summary>
    public static string TotalDuration(DateTime? planoveDokonceni, int celkoveTrvaniDni)
        => planoveDokonceni.HasValue ? ScheduleDurationText.Format(celkoveTrvaniDni) : ScheduleDurationText.NoPlan;

    /// <summary>R7, R8: odchylka kroku v Rozpadu — „bez plánu“, když krok nemá vlastní plán.</summary>
    public static string StepOffset(DateTime? planDatum, int odchylkaDni)
        => !planDatum.HasValue ? KrokBezPlanu
            : odchylkaDni > 0 ? $"+{ScheduleDurationText.Format(odchylkaDni)}"
            : odchylkaDni < 0 ? $"-{ScheduleDurationText.Format(-odchylkaDni)}"
            : ScheduleDurationText.Format(0);

    private static string Zbyva(int pocet)
        => pocet is >= 2 and <= 4 ? $"zbývají {Dni(pocet)}" : $"zbývá {Dni(pocet)}";
}
