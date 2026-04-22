using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public sealed class SqlVyjadreniQueryService : IVyjadreniQueryService
{
    private readonly TicketingReadOnlyDbContext _db;

    public SqlVyjadreniQueryService(TicketingReadOnlyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
        string cislo6, DateTime? sinceUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo6))
            return Array.Empty<HotVyjadreniDto>();

        var query = from v in _db.HotVyjadreni.AsNoTracking()
                    join z in _db.HotZaznamy.AsNoTracking() on v.Pid equals z.Pid
                    where z.Id == cislo6
                    select v;

        if (sinceUtc.HasValue)
        {
            var since = sinceUtc.Value;
            query = query.Where(v => v.Datum > since);
        }

        var rows = await query
            .OrderBy(v => v.Datum)
            .ToListAsync(ct);

        return rows
            .Where(v => v.Datum.HasValue && v.Pid != null)
            .Select(v => new HotVyjadreniDto(
                v.Id,
                v.Typ,
                v.Pid!,
                v.Datum!.Value,
                v.Zpracoval,
                v.Popis,
                v.Tym,
                v.ViditelneDodavateli))
            .ToList();
    }
}
