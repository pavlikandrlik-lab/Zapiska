namespace PmTracker.Web.Services.ServiceDesk.Prodleni;

/// <summary>Kódy předávek 1:1 s <c>PenaltyCalculation.TransferTypes</c> v Hotline.cs. Hodnota zůstává <c>int?</c>,
/// protože <c>ticket_external_workflow.transfertype</c> může nést i jiné kódy, které originál ignoruje.</summary>
public static class TransferTyp
{
    /// <summary>Založení ticketu u dodavatele (u PMP/PNF nese termín plnění).</summary>
    public const int U1 = 1;
    /// <summary>Předání ticketu dodavateli (u PMP/PNF nese termín plnění).</summary>
    public const int U2 = 2;
    /// <summary>Převzetí ticketu od dodavatele — konec obrátky.</summary>
    public const int U3 = 3;
    /// <summary>Archivace u dodavatele.</summary>
    public const int U5 = 5;
    /// <summary>Žádost dodavatele o prodloužení termínu.</summary>
    public const int U10 = 10;
    /// <summary>Schválení změny termínu (nese nový termín).</summary>
    public const int U11 = 11;
}

/// <summary>Jedna předávka z <c>ticket_external_workflow</c>.</summary>
/// <param name="Id">Id řádku workflow — druhý klíč řazení při shodném čase.</param>
/// <param name="Datum">Čas události (<c>date</c>).</param>
/// <param name="Typ">Kód předávky (<c>transfertype</c>), viz <see cref="TransferTyp"/>.</param>
/// <param name="Dodavatel">Dodavatel (<c>supplier</c>), prázdný u událostí bez dodavatele.</param>
/// <param name="TerminParam">Termín plnění (U1/U2) nebo schválený termín (U11) — <c>dt_param</c>.</param>
public sealed record WorkflowUdalost(int Id, DateTime Datum, int? Typ, string? Dodavatel, DateTime? TerminParam);

/// <summary>Vstup výpočtu penále pro jeden ticket.</summary>
/// <param name="TicketId">HOT_ZAZNAMY.id.</param>
/// <param name="TypZaznamu">NES / PMP / PNF (normalizuje se Trim + ToUpperInvariant).</param>
/// <param name="Dulezitost">HOT_ZAZNAMY.dulezitost (jen pro zobrazení a log).</param>
/// <param name="SlaHodiny"><c>ticket_external_sla.sla_hours</c> pro důležitost; null = řádek chybí. Potřebné jen pro NES.</param>
/// <param name="Dodavatel">Dodavatel ticketu (HOT_ZAZNAMY), použije se, když se ticket nedělí mezi dodavatele.</param>
/// <param name="Udalosti">Předávky v libovolném pořadí.</param>
public sealed record PenaltyInput(int TicketId, string TypZaznamu, string? Dulezitost, double? SlaHodiny,
    string? Dodavatel, IReadOnlyList<WorkflowUdalost> Udalosti);
