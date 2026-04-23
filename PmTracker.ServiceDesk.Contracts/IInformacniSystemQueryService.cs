namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Read-only dotazy nad informačními systémy v ServiceDesku.
/// Pokud je <c>Ticketing:Enabled = false</c>, DI poskytuje
/// <c>DisabledInformacniSystemQueryService</c>, který vrací prázdné výsledky.
/// </summary>
public interface IInformacniSystemQueryService
{
    /// <summary>
    /// Seznam aktivních informačních systémů pro dropdown nastavení IS
    /// na projektu. Filtruje se podle <c>aktivita</c> (trimmed).
    /// </summary>
    Task<IReadOnlyList<InformacniSystemDto>> GetAktivniIsAsync(CancellationToken ct);

    /// <summary>
    /// Vrátí tickety v prodlení spadající pod daný informační systém.
    /// Definice prodlení per typ:
    /// <list type="bullet">
    ///   <item><c>NES</c>: <c>sla_deadline &lt; reference</c></item>
    ///   <item><c>PMP</c>/<c>PNF</c>: <c>dat_res_t &lt; reference</c></item>
    /// </list>
    /// Vždy filtruje <c>stav != 'archiv'</c>. Tickety bez <c>HOT_ZAZNAMY.id</c> jsou
    /// mimo scope PM Trackeru a jsou z výsledku vyloučeny. Řazeno od nejstarších
    /// (tj. nejvíce v prodlení) nahoru.
    /// </summary>
    Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
        int isId,
        DateTime reference,
        CancellationToken ct);

    /// <summary>
    /// Rozpočtová metrika IS — limit, čerpání, procento.
    /// </summary>
    Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct);
}
