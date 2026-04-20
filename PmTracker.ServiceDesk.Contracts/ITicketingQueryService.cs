namespace PmTracker.ServiceDesk.Contracts;

public interface ITicketingQueryService
{
    Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct);

    Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct);

    Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct);

    Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct);
}
