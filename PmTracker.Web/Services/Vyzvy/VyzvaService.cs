using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService : IVyzvaService
{
    private readonly PmTrackerDbContext _db;
    private readonly ITicketingQueryService _ticketing;

    public VyzvaService(PmTrackerDbContext db, ITicketingQueryService ticketing)
    {
        _db = db;
        _ticketing = ticketing;
    }

    private Task<int> GetPnfTypIdAsync(CancellationToken ct)
        => VyzvaQueries.GetPnfTypIdAsync(_db, ct);

    private async Task<IReadOnlyDictionary<string, HotZaznamDto>> LoadHotZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotZaznamDto>();
        return await _ticketing.GetZaznamyAsync(cisla, ct);
    }

    private static VyzvaResult<T>.Fail Fail<T>(VyzvaErrorCode code, string msg)
        => new(new VyzvaError(code, msg));

    // Dočasné stuby — nahradí se v Task 28-31.
    public Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
        => throw new NotImplementedException("Task 28");
    public Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
        => throw new NotImplementedException("Task 28");
    public Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
        => throw new NotImplementedException("Task 28");
    public Task<VyzvaResult<VyzvaDetail>> ZaloztVyzvuZBufferuAsync(int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct)
        => throw new NotImplementedException("Task 29");
    public Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
        => throw new NotImplementedException("Task 30");
    public Task<VyzvaResult<Unit>> NastavitZaradidAsync(int externiOdkazId, bool zaradit, CancellationToken ct)
        => throw new NotImplementedException("Task 31");
    public Task<VyzvaResult<Unit>> PrerditPnfAsync(int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
        => throw new NotImplementedException("Task 31");
}
