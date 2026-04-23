using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Fallback implementace vrácená DI když <c>Ticketing:Enabled = false</c>.
/// Symetricky k <see cref="DisabledTicketingQueryService"/>.
/// </summary>
public sealed class DisabledInformacniSystemQueryService : IInformacniSystemQueryService
{
    public Task<IReadOnlyList<InformacniSystemDto>> GetAktivniIsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InformacniSystemDto>>(Array.Empty<InformacniSystemDto>());

    public Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
        int isId, DateTime reference, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ProdlenyTicketDto>>(Array.Empty<ProdlenyTicketDto>());

    public Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct)
        => Task.FromResult<IsRozpocetDto?>(null);
}
