namespace PmTracker.Web.Models.ViewModels.SDConnector;

/// <summary>
/// Admin diagnostická podstránka <c>/SDConnector/Inspect?cislo=XXXXXX</c>:
/// zadá se 6-ciferné ID ticketu (HOT_ZAZNAMY.id) a vedle sebe se zobrazí
/// raw data ze ServiceDesku + user-friendly reprezentace (plain text + klasifikace).
/// </summary>
public sealed class SDConnectorInspectViewModel
{
    public bool TicketingEnabled { get; init; }

    /// <summary>Pokud URL obsahuje validní <c>?cislo=</c>, předvyplníme input; jinak null.</summary>
    public string? InitialCislo { get; init; }
}

/// <summary>
/// JSON shape vracený z <c>GET /SDConnector/Load?cislo=XXXXXX</c>.
/// </summary>
public sealed class SDConnectorLoadResponse
{
    public required bool Nalezeno { get; init; }
    public string? Error { get; init; }
    public SDConnectorRawHeader? Raw { get; init; }
    public IReadOnlyList<SDConnectorBublinaDto>? Bubliny { get; init; }
}

public sealed class SDConnectorRawHeader
{
    public required string Cislo { get; init; }
    public string? TypZaznamu { get; init; }
    public string? Stav { get; init; }
    public string? Strucne { get; init; }
    public string? PopisRaw { get; init; }
    public DateTime? Datum { get; init; }

    /// <summary>Pokud existuje navázaný <c>ZaznamExterniOdkaz</c> pro tento <c>cislo</c>, obsahuje jeho Id — používá se pro button „Otevřít v chat modalu".</summary>
    public int? ExterniOdkazId { get; init; }
}

public sealed class SDConnectorBublinaDto
{
    public required long HotId { get; init; }
    public required string Typ { get; init; }
    public DateTime Datum { get; init; }
    public string? LoginRaw { get; init; }
    public string? AutorDisplayName { get; init; }
    public string? Tym { get; init; }
    public string? PopisRaw { get; init; }
    public required string PopisPlainText { get; init; }

    /// <summary>Klasifikace přes <see cref="PmTracker.Web.Services.ServiceDesk.HarvestPredicates.ClassifyPopis"/> — jedna z K3/K4K7/K6/K10/PlanDodani/None.</summary>
    public string? ClassifiedAs { get; init; }
}
