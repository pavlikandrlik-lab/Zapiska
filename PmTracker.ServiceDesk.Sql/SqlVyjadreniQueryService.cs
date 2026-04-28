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
            .ThenBy(v => v.Id)
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

    public async Task<IReadOnlyDictionary<string, HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(
        IReadOnlyCollection<string> cisla6, CancellationToken ct)
    {
        if (cisla6.Count == 0)
        {
            return new Dictionary<string, HotZaznamFingerprintDto>();
        }

        var distinct = cisla6.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<string, HotZaznamFingerprintDto>();
        }

        var rows = await _db.HotZaznamy.AsNoTracking()
            .Where(z => distinct.Contains(z.Id) && z.Datum.HasValue)
            .Select(z => new { z.Id, z.Datum, z.Stav, z.TypZaznamu, z.SlaDeadline })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => new HotZaznamFingerprintDto(r.Id, r.Datum!.Value, r.Stav, r.TypZaznamu, r.SlaDeadline));
    }

    public async Task<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>> GetVyjadreniSecondaryFingerprintsAsync(
        IReadOnlyCollection<string> cisla6, CancellationToken ct)
    {
        if (cisla6.Count == 0)
        {
            return new Dictionary<string, VyjadreniSecondaryFingerprintDto>();
        }

        var distinct = cisla6.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<string, VyjadreniSecondaryFingerprintDto>();
        }

        // Agreguj per cislo6: MAX(HOT_VYJADRENI.id) + COUNT(*).
        // Join na HOT_ZAZNAMY přes pid (ne přes id).
        var rows = await (from v in _db.HotVyjadreni.AsNoTracking()
                          join z in _db.HotZaznamy.AsNoTracking() on v.Pid equals z.Pid
                          where distinct.Contains(z.Id)
                          group v by z.Id into g
                          select new
                          {
                              Cislo = g.Key,
                              MaxId = g.Max(x => x.Id),
                              Count = g.Count()
                          }).ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Cislo,
            r => new VyjadreniSecondaryFingerprintDto(r.Cislo, r.MaxId, r.Count));
    }
}
