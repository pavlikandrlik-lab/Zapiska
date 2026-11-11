using System.Globalization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>Jak období vzniklo — určuje, co je „předchozí období" (spec 2026-10-15 §11.3).</summary>
public enum ObdobiDruh { Rok, Kvartal, Rozsah }

/// <summary>
/// Časové období reportu. Start/End jsou inkluzivní (End = konec dne 23:59:59).
/// Záznam „aktivní v období" = překrývá ⟨Start, End⟩ (přesný predikát řeší konkrétní grafy).
/// </summary>
public sealed record Obdobi(DateTime Start, DateTime End, string Label)
{
    public ObdobiDruh Druh { get; init; } = ObdobiDruh.Rozsah;

    private static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddSeconds(-1);

    public static Obdobi Rok(int year)
        => new(new DateTime(year, 1, 1), EndOfDay(new DateTime(year, 12, 31)), year.ToString(CultureInfo.InvariantCulture))
        {
            Druh = ObdobiDruh.Rok
        };

    public static Obdobi Kvartal(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "Kvartál musí být 1–4.");
        }

        var startMonth = (quarter - 1) * 3 + 1;
        var start = new DateTime(year, startMonth, 1);
        var end = EndOfDay(start.AddMonths(3).AddDays(-1));
        return new Obdobi(start, end, $"Q{quarter} {year}") { Druh = ObdobiDruh.Kvartal };
    }

    public static Obdobi Rozsah(DateTime start, DateTime end)
        => new(start.Date, EndOfDay(end), $"{start:dd.MM.yyyy} – {end:dd.MM.yyyy}");

    /// <summary>
    /// Předchozí období stejné délky pro trendy: rok → předchozí rok, kvartál → předchozí kvartál
    /// (Q1 → Q4 předchozího roku), rozsah → stejně dlouhý rozsah končící den před začátkem.
    /// </summary>
    public Obdobi Predchozi()
    {
        switch (Druh)
        {
            case ObdobiDruh.Rok:
                return Rok(Start.Year - 1);
            case ObdobiDruh.Kvartal:
            {
                var predchozi = Start.AddMonths(-3);
                return Kvartal(predchozi.Year, (predchozi.Month - 1) / 3 + 1);
            }
            default:
            {
                var dni = (End.Date - Start.Date).Days + 1;
                var konec = Start.Date.AddDays(-1);
                return Rozsah(konec.AddDays(-(dni - 1)), konec);
            }
        }
    }
}
