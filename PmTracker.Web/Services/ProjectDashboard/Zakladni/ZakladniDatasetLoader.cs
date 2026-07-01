using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Načte sdílený snapshot základního reportu jedním průchodem DB. Coarse-ohraničeno
/// projektem + obdobím (DatumZalozeni &lt;= End). Přesný „aktivní v období" predikát
/// (dle data dokončení z historie stavů) aplikují konkrétní grafy.
/// </summary>
public sealed class ZakladniDatasetLoader : IZakladniDatasetLoader
{
    private readonly PmTrackerDbContext _db;
    public ZakladniDatasetLoader(PmTrackerDbContext db) => _db = db;

    public async Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct)
    {
        var projektNazev = await _db.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => p.CelyNazev)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var records = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(r => r.ProjektId == projektId && r.DatumZalozeni <= obdobi.End)
            .Select(r => new DatasetRecord(r.Id, r.SubsystemId, r.StavUkoluId, r.DatumZalozeni, r.DatumUkonceni, null))
            .ToListAsync(ct);

        var recordIds = records.Select(r => r.Id).ToList();

        var subsystemy = await _db.ProjektSubsystemy.AsNoTracking()
            .Where(ps => ps.ProjektId == projektId && !ps.DatumOdebrani.HasValue)
            .Join(_db.Subsystemy.AsNoTracking(), ps => ps.SubsystemId, s => s.Id,
                (ps, s) => new DatasetSubsystem(s.Id, s.Kod, s.Nazev))
            .ToListAsync(ct);

        var stavy = await _db.CiselnikStavuUkolu.AsNoTracking()
            .Select(s => new DatasetState(s.Id, s.Kod, s.Nazev, s.IsFinal))
            .ToListAsync(ct);

        var vyjadreni = await _db.Vyjadreni.AsNoTracking()
            .Where(v => recordIds.Contains(v.ZaznamId)
                && v.DatumVyjadreni >= obdobi.Start && v.DatumVyjadreni <= obdobi.End)
            .Select(v => new DatasetVyjadreni(v.Id, v.ZaznamId, v.DatumVyjadreni))
            .ToListAsync(ct);

        var terminChanges = await _db.ZaznamHistorieTerminu.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId))
            .Select(h => new DatasetTerminChange(h.ZaznamId, h.DatumZmeny, h.PuvodniDatum, h.NoveDatum))
            .ToListAsync(ct);

        // Datum dokončení = poslední přechod do koncového (final) stavu z historie stavů záznamu.
        // Záznam, který do žádného final stavu nevstoupil, zůstává null (stále otevřený).
        var finalStateIds = stavy.Where(s => s.IsFinal).Select(s => s.Id).ToList();
        var dokonceniByRecord = await _db.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId) && finalStateIds.Contains(h.NovyStav))
            .GroupBy(h => h.ZaznamId)
            .Select(g => new { ZaznamId = g.Key, Datum = g.Max(x => x.DatumZmeny) })
            .ToListAsync(ct);
        var dokonceniMap = dokonceniByRecord.ToDictionary(x => x.ZaznamId, x => x.Datum);
        records = records
            .Select(r => dokonceniMap.TryGetValue(r.Id, out var d) ? r with { DatumDokonceni = d } : r)
            .ToList();

        return new ZakladniDataset
        {
            Obdobi = obdobi,
            ProjektNazev = projektNazev,
            Records = records,
            Subsystemy = subsystemy,
            Stavy = stavy,
            Vyjadreni = vyjadreni,
            TerminChanges = terminChanges
        };
    }
}
