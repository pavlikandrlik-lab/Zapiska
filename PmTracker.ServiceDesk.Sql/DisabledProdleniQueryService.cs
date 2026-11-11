using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Fallback bez napojení na databázi ServiceDesku (<c>Ticketing:Enabled = false</c> nebo prázdný connection string
/// <c>ServiceDeskExternalReadOnly</c>). <see cref="Zapnuto"/> = false → dashboard ukáže placeholder, ne „nic v prodlení".
/// </summary>
public sealed class DisabledProdleniQueryService : IProdleniQueryService
{
    public bool Zapnuto => false;

    public Task<IReadOnlyList<ProdleniTicketDto>> GetSynchronizovaneTicketyAsync(int isId, DateTime? aktivniOd, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ProdleniTicketDto>>([]);

    public Task<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>> GetWorkflowAsync(
        IReadOnlyCollection<int> ticketExternalIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>>(new Dictionary<int, IReadOnlyList<WorkflowUdalostDto>>());

    public Task<IReadOnlyList<SlaKatalogDto>> GetSlaKatalogAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SlaKatalogDto>>([]);

    public Task<IReadOnlyList<AkceptovanaKalkulaceDto>> GetAkceptovaneKalkulaceAsync(int isId, DateTime od, DateTime @do, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AkceptovanaKalkulaceDto>>([]);
}
