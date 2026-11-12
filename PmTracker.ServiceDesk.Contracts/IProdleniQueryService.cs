namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Data pro výpočet prodlení ticketů podle penalizačního algoritmu starého ServiceDesku (spec 2026-10-15 §6–7).
/// Tickety a jejich IS jsou v intranetNEW, předávky a SLA hodiny v databázi ServiceDesku; spojení běží v paměti.
/// Bez <c>Ticketing:Enabled</c> nebo bez connection stringu <c>ServiceDeskExternalReadOnly</c> poskytuje DI
/// <c>DisabledProdleniQueryService</c> (<see cref="Zapnuto"/> = false).
/// </summary>
public interface IProdleniQueryService
{
    /// <summary>false = prodlení se nepočítá (chybí napojení na databázi ServiceDesku); UI ukáže placeholder.</summary>
    bool Zapnuto { get; }

    /// <summary>
    /// Tickety IS (přes HOT_MODULY) s řádkem v <c>ticket_external</c>. Vždy nearchivované; když
    /// <paramref name="aktivniOd"/> není null, navíc archivované, které mají předávku s datem ≥ <paramref name="aktivniOd"/>
    /// (Statistiky za období). Ticket bez <c>HOT_ZAZNAMY.id</c> a nečíselné <c>external_id</c> se vynechají.
    /// </summary>
    Task<IReadOnlyList<ProdleniTicketDto>> GetSynchronizovaneTicketyAsync(int isId, DateTime? aktivniOd, CancellationToken ct);

    /// <summary>Předávky podle <c>ticket_external.id</c> (chunk po 1 000 id). Ticket bez předávek v slovníku chybí.</summary>
    Task<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>> GetWorkflowAsync(
        IReadOnlyCollection<int> ticketExternalIds, CancellationToken ct);

    /// <summary>Celá tabulka <c>ticket_external_sla</c> beze změny hodnot (ořez a párování dělá volající, spec R8).</summary>
    Task<IReadOnlyList<SlaKatalogDto>> GetSlaKatalogAsync(CancellationToken ct);

    /// <summary>
    /// Akceptované kalkulace ticketů IS (i archivovaných): na ticket jedna — z akceptovaných (zákazový seznam)
    /// ta s nejvyšším id, jako u výzev — a jen když její <c>datum_akceptace</c> leží v ⟨<paramref name="od"/>, <paramref name="do"/>⟩.
    /// </summary>
    Task<IReadOnlyList<AkceptovanaKalkulaceDto>> GetAkceptovaneKalkulaceAsync(int isId, DateTime od, DateTime @do, CancellationToken ct);
}
