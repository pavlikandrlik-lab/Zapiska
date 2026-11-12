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
    /// Rozpočtová metrika IS — limit, čerpání, procento.
    /// </summary>
    Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct);
}
