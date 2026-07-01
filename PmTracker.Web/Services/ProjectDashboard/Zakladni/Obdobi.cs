using System.Globalization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Časové období reportu. Start/End jsou inkluzivní (End = konec dne 23:59:59).
/// Záznam „aktivní v období" = překrývá ⟨Start, End⟩ (přesný predikát řeší konkrétní grafy).
/// </summary>
public sealed record Obdobi(DateTime Start, DateTime End, string Label)
{
    private static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddSeconds(-1);

    public static Obdobi Rok(int year)
        => new(new DateTime(year, 1, 1), EndOfDay(new DateTime(year, 12, 31)), year.ToString(CultureInfo.InvariantCulture));

    public static Obdobi Kvartal(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "Kvartál musí být 1–4.");
        }

        var startMonth = (quarter - 1) * 3 + 1;
        var start = new DateTime(year, startMonth, 1);
        var end = EndOfDay(start.AddMonths(3).AddDays(-1));
        return new Obdobi(start, end, $"Q{quarter} {year}");
    }

    public static Obdobi Rozsah(DateTime start, DateTime end)
        => new(start.Date, EndOfDay(end), $"{start:dd.MM.yyyy} – {end:dd.MM.yyyy}");
}
