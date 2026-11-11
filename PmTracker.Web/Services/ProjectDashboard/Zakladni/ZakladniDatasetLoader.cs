using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Načte sdílený snapshot reportu Statistik jedním průchodem (PM DB + služby ServiceDesku).
/// Záznamy jsou coarse-ohraničené projektem a koncem období (DatumZalozeni &lt;= End), takže pokrývají
/// i předchozí období pro trendy. Přesné predikáty období aplikují providery.
/// </summary>
public sealed class ZakladniDatasetLoader : IZakladniDatasetLoader
{
    private const string PritomenKod = "PRESENT";

    private readonly PmTrackerDbContext _db;
    private readonly IProdleniService _prodleni;
    private readonly IProdleniQueryService _prodleniQuery;
    private readonly IInformacniSystemQueryService _isQuery;
    private readonly TimeProvider _time;
    private readonly ILogger<ZakladniDatasetLoader> _logger;

    public ZakladniDatasetLoader(PmTrackerDbContext db, IProdleniService prodleni, IProdleniQueryService prodleniQuery,
        IInformacniSystemQueryService isQuery, TimeProvider time, ILogger<ZakladniDatasetLoader> logger)
    {
        _db = db;
        _prodleni = prodleni;
        _prodleniQuery = prodleniQuery;
        _isQuery = isQuery;
        _time = time;
        _logger = logger;
    }

    public async Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct)
    {
        var projekt = await _db.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => new { p.CelyNazev, p.ServiceDeskInfoSystemId })
            .FirstOrDefaultAsync(ct);

        var records = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(r => r.ProjektId == projektId && r.DatumZalozeni <= obdobi.End)
            .Select(r => new DatasetRecord(r.Id, r.SubsystemId, r.StavUkoluId, r.DatumZalozeni, r.DatumUkonceni, null,
                r.KategorieId, r.VlastnikId))
            .ToListAsync(ct);

        var recordIds = records.Select(r => r.Id).ToList();

        // Stejné pravidlo jako záložka Záznamy: Úkol má kód U (produkce i dev seed) nebo UKOL.
        var taskCategoryIds = await _db.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(c => c.Kod == RecordCategoryCodes.TaskShort || c.Kod == RecordCategoryCodes.Task)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var vlastnikIds = records.Select(r => r.VlastnikId).Distinct().ToList();
        var vlastnici = await _db.Osoby.AsNoTracking()
            .Where(o => vlastnikIds.Contains(o.Id))
            .Select(o => new { o.Id, o.Jmeno, o.Prijmeni })
            .ToListAsync(ct);

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

        // Datum dokončení = datum platnosti posledního přechodu do koncového (final) stavu
        // (spec 2026-10-13 §4.9). Záznam, který do žádného final stavu nevstoupil, zůstává null.
        var finalStateIds = stavy.Where(s => s.IsFinal).Select(s => s.Id).ToList();
        var dokonceniByRecord = await _db.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId) && finalStateIds.Contains(h.NovyStav))
            .GroupBy(h => h.ZaznamId)
            .Select(g => new { ZaznamId = g.Key, Datum = g.Max(x => x.PlatiOd) })
            .ToListAsync(ct);
        var dokonceniMap = dokonceniByRecord.ToDictionary(x => x.ZaznamId, x => x.Datum);
        records = records
            .Select(r => dokonceniMap.TryGetValue(r.Id, out var d) ? r with { DatumDokonceni = d } : r)
            .ToList();

        // Jednání období + předchozího období (trend účasti).
        var jednaniOd = obdobi.Predchozi().Start;
        var jednani = await _db.Jednani.AsNoTracking()
            .Where(j => j.ProjektId == projektId && j.DatumPlanovane >= jednaniOd && j.DatumPlanovane <= obdobi.End)
            .Select(j => new DatasetJednani(j.Id, j.DatumPlanovane))
            .ToListAsync(ct);
        var jednaniIds = jednani.Select(j => j.Id).ToList();
        var pritomenId = await _db.CiselnikStavuUcasti.AsNoTracking()
            .Where(s => s.Kod == PritomenKod)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
        var ucasti = await _db.Ucast.AsNoTracking()
            .Where(u => jednaniIds.Contains(u.JednaniId))
            .Select(u => new { u.JednaniId, u.StavUcastiId })
            .ToListAsync(ct);

        var serviceDesk = projekt?.ServiceDeskInfoSystemId is int isId
            ? await NactiServiceDeskAsync(projektId, isId, obdobi, ct)
            : null;

        return new ZakladniDataset
        {
            Obdobi = obdobi,
            ProjektNazev = projekt?.CelyNazev ?? string.Empty,
            Records = records,
            Subsystemy = subsystemy,
            Stavy = stavy,
            Vyjadreni = vyjadreni,
            TerminChanges = terminChanges,
            Dnes = _time.GetLocalNow().DateTime,
            TaskCategoryIds = taskCategoryIds,
            Vlastnici = vlastnici
                .Select(o => new DatasetOsoba(o.Id, $"{o.Jmeno} {o.Prijmeni}".Trim()))
                .ToList(),
            Jednani = jednani,
            Ucasti = ucasti.Select(u => new DatasetUcast(u.JednaniId, u.StavUcastiId == pritomenId)).ToList(),
            ServiceDeskInfoSystemId = projekt?.ServiceDeskInfoSystemId,
            ServiceDesk = serviceDesk,
        };
    }

    /// <summary>
    /// Slice ServiceDesku. Bez databáze ServiceDesku (BezNapojeni) sekce není; výpadek se drží ve stavu
    /// přehledu, aby sekce ukázala chybovou kartu a ostatní sekce reportu se vykreslily dál (spec §13).
    /// </summary>
    private async Task<ServiceDeskSlice?> NactiServiceDeskAsync(int projektId, int isId, Obdobi obdobi, CancellationToken ct)
    {
        var prodleni = await _prodleni.GetPrehledProObdobiAsync(projektId, obdobi.Start, ct);
        switch (prodleni.Stav)
        {
            case ProdleniStavDat.BezNapojeni:
                return null;
            case ProdleniStavDat.Nedostupne:
                return new ServiceDeskSlice(prodleni, null, []);
        }

        try
        {
            var rozpocet = await _isQuery.GetRozpocetAsync(isId, ct);
            var kalkulace = await _prodleniQuery.GetAkceptovaneKalkulaceAsync(isId, obdobi.Start, obdobi.End, ct);
            return new ServiceDeskSlice(prodleni, rozpocet, kalkulace);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Statistiky: rozpočet nebo kalkulace IS {IsId} nejdou načíst.", isId);
            return new ServiceDeskSlice(ProdleniPrehled.Prazdny(ProdleniStavDat.Nedostupne, isId, prodleni.IsZkratka), null, []);
        }
    }
}
