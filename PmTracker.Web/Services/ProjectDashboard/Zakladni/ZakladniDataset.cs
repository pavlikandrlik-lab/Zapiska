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
    DateTime? DatumDokonceni = null,
    int KategorieId = 0,
    int VlastnikId = 0);

/// <summary>Vlastník záznamu (osa grafu vytížení).</summary>
public sealed record DatasetOsoba(int Id, string Jmeno);

/// <summary>Jednání projektu v období nebo v předchozím období (trend účasti).</summary>
public sealed record DatasetJednani(int Id, DateTime Datum);

/// <summary>Jedna pozvánka na jednání; <paramref name="Pritomen"/> = stav účasti PRESENT.</summary>
public sealed record DatasetUcast(int JednaniId, bool Pritomen);

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

    /// <summary>Okamžik sestavení (místní čas) — „otevřeno k dnešku", stáří, po termínu.</summary>
    public DateTime Dnes { get; init; }

    /// <summary>Kategorie Úkol (kód U nebo UKOL). Úkolové grafy pracují jen s nimi (spec §11.2).</summary>
    public IReadOnlyCollection<int> TaskCategoryIds { get; init; } = [];

    public IReadOnlyList<DatasetOsoba> Vlastnici { get; init; } = [];
    public IReadOnlyList<DatasetJednani> Jednani { get; init; } = [];
    public IReadOnlyList<DatasetUcast> Ucasti { get; init; } = [];

    /// <summary>IS projektu (projekty.servicedesk_info_system_id); rozliší „bez IS" od „bez databáze ServiceDesku".</summary>
    public int? ServiceDeskInfoSystemId { get; init; }

    /// <summary>Data ServiceDesku; null = projekt bez IS nebo aplikace bez databáze ServiceDesku.</summary>
    public ServiceDeskSlice? ServiceDesk { get; init; }

    /// <summary>Záznamy kategorie Úkol — Informace a Rozhodnutí nemají stav úkolu a zkreslovaly by grafy.</summary>
    public IEnumerable<DatasetRecord> Ukoly => Records.Where(r => TaskCategoryIds.Contains(r.KategorieId));
}
