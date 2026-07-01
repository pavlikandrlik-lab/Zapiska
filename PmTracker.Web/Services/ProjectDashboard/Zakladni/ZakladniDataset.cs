namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

// Pozn.: ProjektovyZaznamEntity.DatumUkonceni je NON-nullable → DatumUkonceni zde DateTime.
// DatumUkonceni = TERMÍN (deadline), NE datum dokončení. DatumDokonceni = kdy záznam vstoupil
// do koncového stavu (z historie stavů); null = stále otevřený. Potřebuje ho termínová disciplína.
public sealed record DatasetRecord(
    int Id,
    int SubsystemId,
    int? StavId,
    DateTime DatumZalozeni,
    DateTime DatumUkonceni,
    DateTime? DatumDokonceni = null);

public sealed record DatasetSubsystem(int Id, string Kod, string Nazev);

public sealed record DatasetState(int Id, string Kod, string Nazev, bool IsFinal);

public sealed record DatasetVyjadreni(int Id, int ZaznamId, DateTime DatumVyjadreni);

// Mapuje ZaznamHistorieTerminuEntity: DatumZmeny / PuvodniDatum / NoveDatum.
public sealed record DatasetTerminChange(int ZaznamId, DateTime DatumZmeny, DateTime PuvodniTermin, DateTime NovyTermin);

/// <summary>
/// Sdílený snapshot základního reportu — načte se jedním průchodem DB
/// (<see cref="IZakladniDatasetLoader"/>) a předá se všem providerům.
/// Providery z něj jen čtou; do DB nesahají.
/// </summary>
public sealed record ZakladniDataset
{
    public required Obdobi Obdobi { get; init; }

    /// <summary>Celý název projektu — hlavička reportu (jen název, viz spec revize 2026-06-30).</summary>
    public string ProjektNazev { get; init; } = string.Empty;

    public IReadOnlyList<DatasetRecord> Records { get; init; } = [];
    public IReadOnlyList<DatasetSubsystem> Subsystemy { get; init; } = [];
    public IReadOnlyList<DatasetState> Stavy { get; init; } = [];
    public IReadOnlyList<DatasetVyjadreni> Vyjadreni { get; init; } = [];
    public IReadOnlyList<DatasetTerminChange> TerminChanges { get; init; } = [];
}
