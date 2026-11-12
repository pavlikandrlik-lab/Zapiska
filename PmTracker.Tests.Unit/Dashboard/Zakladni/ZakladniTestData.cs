using PmTracker.Web.Services.ProjectDashboard.Zakladni;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>Společný základ golden testů providerů Statistik: Q1 2026, dnes 31. 3. 2026, číselníky stavů a kategorií.</summary>
internal static class ZakladniTestData
{
    public const int Ukol = 1;
    public const int Informace = 2;

    public const int Novy = 10;
    public const int Rozpracovany = 20;
    public const int Hotovy = 30;
    public const int Zruseny = 40;

    public static readonly DateTime Dnes = new(2026, 3, 31, 12, 0, 0);

    public static readonly IReadOnlyList<DatasetState> Stavy =
    [
        new(Novy, "NEW", "Nový", IsFinal: false),
        new(Rozpracovany, "RUN", "Rozpracováno", IsFinal: false),
        new(Hotovy, "DONE", "Hotovo", IsFinal: true),
        new(Zruseny, "CANCEL", "Zrušeno", IsFinal: true),
    ];

    public static readonly IReadOnlyList<DatasetSubsystem> Subsystemy =
    [
        new(1, "A", "Subsystém A"),
        new(2, "B", "Subsystém B"),
    ];

    public static ZakladniDataset Dataset(IEnumerable<DatasetRecord>? records = null, Obdobi? obdobi = null) => new()
    {
        Obdobi = obdobi ?? Obdobi.Kvartal(2026, 1),
        Dnes = Dnes,
        TaskCategoryIds = [Ukol],
        Stavy = Stavy,
        Subsystemy = Subsystemy,
        Records = (records ?? []).ToList(),
    };

    private static int _id = 1000;

    /// <summary>Otevřený úkol s termínem.</summary>
    public static DatasetRecord Otevreny(DateTime termin, DateTime? zalozeno = null, int vlastnik = 1, int subsystem = 1,
        int kategorie = Ukol, int stav = Rozpracovany)
        => new(Interlocked.Increment(ref _id), subsystem, stav, zalozeno ?? new DateTime(2026, 1, 5), termin, null,
            kategorie, vlastnik);

    /// <summary>Hotový úkol dokončený v daný den.</summary>
    public static DatasetRecord Hotovy_(DateTime dokonceno, DateTime? termin = null, DateTime? zalozeno = null, int subsystem = 1,
        int stav = Hotovy)
        => new(Interlocked.Increment(ref _id), subsystem, stav, zalozeno ?? new DateTime(2025, 6, 1), termin ?? dokonceno,
            dokonceno, Ukol, 1);
}
