using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
    {
        var pnfTypId = await GetPnfTypIdAsync(ct);

        var raw = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .WhereVBufferuProjektu(_db, projektId, pnfTypId)
            .Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.PredpokladanaCena })
            .ToListAsync(ct);

        if (raw.Count == 0) return Array.Empty<VyzvaBufferItem>();

        var cisla = raw.Select(r => r.Cislo).Distinct().ToArray();
        var hot = await LoadHotZaznamyAsync(cisla, ct);

        return raw.Select(r => new VyzvaBufferItem(
            r.Id, r.ZaznamId, r.Cislo,
            hot.GetValueOrDefault(r.Cislo)?.Strucne,
            r.PredpokladanaCena)).ToList();
    }

    public async Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
    {
        var vyzvy = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId)
            .OrderByDescending(v => v.DatumZalozeni)
            .ToListAsync(ct);
        if (vyzvy.Count == 0) return Array.Empty<VyzvaDetail>();

        var vyzvaIds = vyzvy.Select(v => v.Id).ToArray();
        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId.HasValue && vyzvaIds.Contains(ev.VyzvaId.Value))
            .OrderBy(ev => ev.Id)
            .ToListAsync(ct);

        var polozkyMap = polozky.GroupBy(p => p.VyzvaId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var allCisla = polozky.Select(p => p.Cislo).Distinct().ToArray();
        var hot = await LoadHotZaznamyAsync(allCisla, ct);

        return vyzvy.Select(v => MapToDetail(v, polozkyMap.GetValueOrDefault(v.Id) ?? new(), hot)).ToList();
    }

    public async Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null) return null;

        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId == vyzvaId)
            .OrderBy(ev => ev.Id)
            .ToListAsync(ct);

        var hot = await LoadHotZaznamyAsync(polozky.Select(p => p.Cislo).Distinct().ToArray(), ct);
        return MapToDetail(vyzva, polozky, hot);
    }

    private static VyzvaDetail MapToDetail(
        VyzvaEntity v,
        IReadOnlyList<ZaznamExterniOdkazEntity> polozky,
        IReadOnlyDictionary<string, HotZaznamDto> hot)
        => new(
            v.Id, v.ProjektId, v.Kod, v.Rok, v.PoradoveVRoce, v.Stav,
            v.DatumZalozeni, v.ZalozilOsobaId, v.DatumOdeslani, v.OdeslalOsobaId,
            v.MistoPlneniSnapshot, v.CisloRamcoveSmlouvySnapshot,
            polozky.Select(ev => new VyzvaDetailItem(
                ev.Id, ev.ZaznamId, ev.Cislo,
                hot.GetValueOrDefault(ev.Cislo)?.Strucne,
                ev.PredpokladanaCena)).ToList());
}
