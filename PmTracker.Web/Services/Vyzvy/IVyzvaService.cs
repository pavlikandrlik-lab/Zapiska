using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public interface IVyzvaService
{
    Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct);
    Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct);
    Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct);

    Task<VyzvaResult<VyzvaDetail>> ZaloztVyzvuZBufferuAsync(
        int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct);

    Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(
        int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct);

    Task<VyzvaResult<Unit>> NastavitZaradidAsync(
        int externiOdkazId, bool zaradit, CancellationToken ct);

    Task<VyzvaResult<Unit>> PrerditPnfAsync(
        int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct);
}
