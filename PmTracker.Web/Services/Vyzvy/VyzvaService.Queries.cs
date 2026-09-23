using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<int?> ResolveExterniOdkazProjektIdAsync(int externiOdkazId, CancellationToken ct)
    {
        // Resolve via ZaznamExterniOdkaz → ProjektovyZaznam → ProjektId.
        return await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                      join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                      where eo.Id == externiOdkazId
                      select (int?)z.ProjektId)
                     .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
    {
        var pnfTypId = await GetPnfTypIdAsync(ct);

        var raw = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .WhereVBufferuProjektu(_db, projektId, pnfTypId)
            .Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.PredpokladanaCena, ev.KalkulaceCena })
            .ToListAsync(ct);

        if (raw.Count == 0) return Array.Empty<VyzvaBufferItem>();

        var cisla = raw.Select(r => r.Cislo).Distinct().ToArray();
        var hot = await LoadHotZaznamyAsync(cisla, ct);

        return raw.Select(r => new VyzvaBufferItem(
            r.Id, r.ZaznamId, r.Cislo,
            hot.GetValueOrDefault(r.Cislo)?.Strucne,
            r.PredpokladanaCena,
            r.KalkulaceCena)).ToList();
    }

    public async Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, int rok, CancellationToken ct)
    {
        var vyzvy = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId && v.Rok == rok)
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

    public async Task<IReadOnlyList<int>> GetRokyAsync(int projektId, CancellationToken ct)
        => await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId)
            .Select(v => v.Rok)
            .Distinct()
            .OrderByDescending(r => r)
            .ToListAsync(ct);

    public Task<string?> GetCisloRamcoveSmlouvyAsync(int projektId, CancellationToken ct)
        => _db.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => p.CisloRamcoveSmlouvy)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Obsazená pořadová čísla — nápověda do formuláře Nová výzva. Unikátnost drží
    /// index ux_vyzvy_smlouva_rok_poradove, tedy per rámcová smlouva a rok, ne per projekt:
    /// dva projekty na téže smlouvě sdílejí jednu číselnou řadu.
    /// </summary>
    public async Task<IReadOnlyList<int>> GetObsazenaCislaAsync(int projektId, int rok, CancellationToken ct)
    {
        var cisloSmlouvy = await _db.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => p.CisloRamcoveSmlouvy)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(cisloSmlouvy)) return Array.Empty<int>();

        return await _db.Vyzvy.AsNoTracking()
            .Where(v => v.CisloRamcoveSmlouvySnapshot == cisloSmlouvy && v.Rok == rok)
            .Select(v => v.PoradoveVRoce)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
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
                ev.PredpokladanaCena,
                ev.KalkulaceCena)).ToList());
}
