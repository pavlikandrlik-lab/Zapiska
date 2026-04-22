using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public sealed class DisabledVyjadreniQueryService : IVyjadreniQueryService
{
    private readonly ILogger<DisabledVyjadreniQueryService> _logger;

    public DisabledVyjadreniQueryService(ILogger<DisabledVyjadreniQueryService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
        string cislo6, DateTime? sinceUtc, CancellationToken ct)
    {
        _logger.LogWarning(
            "ServiceDesk integrace je vypnutá (Ticketing:Enabled=false), GetVyjadreniForTicketAsync vrací prázdný výsledek.");
        return Task.FromResult<IReadOnlyList<HotVyjadreniDto>>(Array.Empty<HotVyjadreniDto>());
    }
}
