using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public sealed class DisabledTicketingQueryService : ITicketingQueryService
{
    private static readonly IReadOnlyDictionary<string, HotZaznamDto> EmptyZaznamy
        = new Dictionary<string, HotZaznamDto>();
    private static readonly IReadOnlyDictionary<string, HotKalkulaceDto> EmptyKalkulace
        = new Dictionary<string, HotKalkulaceDto>();

    private readonly ILogger<DisabledTicketingQueryService> _logger;

    public DisabledTicketingQueryService(ILogger<DisabledTicketingQueryService> logger)
    {
        _logger = logger;
    }

    public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        LogDisabled(nameof(GetZaznamAsync));
        return Task.FromResult<HotZaznamDto?>(null);
    }

    public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        LogDisabled(nameof(GetZaznamyAsync));
        return Task.FromResult(EmptyZaznamy);
    }

    public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        LogDisabled(nameof(GetAkceptovanouKalkulaciAsync));
        return Task.FromResult<HotKalkulaceDto?>(null);
    }

    public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        LogDisabled(nameof(GetAkceptovaneKalkulaceAsync));
        return Task.FromResult(EmptyKalkulace);
    }

    private void LogDisabled(string operation)
    {
        _logger.LogWarning(
            "ServiceDesk integrace je vypnutá (Ticketing:Enabled=false), operace {Operation} vrací prázdný výsledek.",
            operation);
    }
}
