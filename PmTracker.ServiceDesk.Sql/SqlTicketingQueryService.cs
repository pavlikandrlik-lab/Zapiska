using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

public sealed class SqlTicketingQueryService : ITicketingQueryService
{
    private const string AkceptovanoStav = "Akceptováno";
    private readonly TicketingReadOnlyDbContext _db;

    public SqlTicketingQueryService(TicketingReadOnlyDbContext db)
    {
        _db = db;
    }

    public async Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        var z = await _db.HotZaznamy.FirstOrDefaultAsync(r => r.Id == cislo, ct);
        return z == null ? null : MapZaznam(z);
    }

    public async Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotZaznamDto>();

        var cislaArr = cisla.Distinct().ToArray();
        var raw = await _db.HotZaznamy
            .Where(r => cislaArr.Contains(r.Id))
            .ToListAsync(ct);
        return raw.ToDictionary(r => r.Id, MapZaznam);
    }

    public async Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        var k = await _db.HotKalkulace
            .Where(x => x.Pid == cislo && x.Akceptace == AkceptovanoStav)
            .OrderByDescending(x => x.Verze)
            .FirstOrDefaultAsync(ct);
        return k == null ? null : MapKalkulace(k);
    }

    public async Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotKalkulaceDto>();

        var cislaArr = cisla.Distinct().ToArray();
        var raw = await _db.HotKalkulace
            .Where(x => x.Pid != null && cislaArr.Contains(x.Pid) && x.Akceptace == AkceptovanoStav)
            .ToListAsync(ct);

        return raw
            .GroupBy(x => x.Pid!)
            .ToDictionary(g => g.Key, g => MapKalkulace(g.OrderByDescending(k => k.Verze).First()));
    }

    private static HotZaznamDto MapZaznam(HotZaznamEntity e)
        => new(e.Id, e.TypZaznamu, e.Strucne, e.Popis, e.Uzivatel, e.Subsystem, e.Modul);

    private static HotKalkulaceDto MapKalkulace(HotKalkulaceEntity k)
        => new(
            k.Id, k.Pid ?? string.Empty, k.Verze,
            k.PracnostA, k.SazbaA, k.CenaA,
            k.PracnostP, k.SazbaP, k.CenaP,
            k.PracnostT, k.SazbaT, k.CenaT,
            k.PracnostI, k.SazbaI, k.CenaI,
            k.Cena,
            k.PocetL, k.SazbaL, k.CenaL, k.RozpadLicence,
            k.Termin, k.TextTermin);
}
