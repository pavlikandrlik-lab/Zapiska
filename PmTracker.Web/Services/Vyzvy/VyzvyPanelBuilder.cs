using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

/// <summary>
/// Skládá panel Výzev (rail + obsah) z IVyzvaService. Rozhraní existuje kvůli
/// testovatelnosti volajících — ProjektyController ho fakuje v unit testech.
/// </summary>
public interface IVyzvyPanelBuilder
{
    Task<VyzvyPanelViewModel> BuildAsync(
        int projektId, bool muzeEditovat, bool muzeTisknout, int? rok, CancellationToken ct);
}

public sealed class VyzvyPanelBuilder : IVyzvyPanelBuilder
{
    private readonly IVyzvaService _vyzvaService;
    private readonly PmTrackerDbContext _db;

    public VyzvyPanelBuilder(IVyzvaService vyzvaService, PmTrackerDbContext db)
    {
        _vyzvaService = vyzvaService;
        _db = db;
    }

    public async Task<VyzvyPanelViewModel> BuildAsync(
        int projektId, bool muzeEditovat, bool muzeTisknout, int? rok, CancellationToken ct)
    {
        var projekt = await _db.Projekty.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projektId, ct);

        var roky = await _vyzvaService.GetRokyAsync(projektId, ct);
        var aktualniRok = DateTime.Now.Year;

        // Aktuální rok je v nabídce vždy, i když v něm zatím žádná výzva není —
        // jinak by nešlo založit první výzvu roku.
        var dostupneRoky = roky.Contains(aktualniRok)
            ? roky
            : roky.Concat(new[] { aktualniRok }).OrderByDescending(r => r).ToList();

        var vybranyRok = rok is int r && dostupneRoky.Contains(r) ? r : aktualniRok;

        var bufferItems = await _vyzvaService.GetBufferAsync(projektId, ct);
        var vyzvy = await _vyzvaService.GetVyzvyAsync(projektId, vybranyRok, ct);

        var jmenaOsob = await LoadOsobaNamesAsync(vyzvy, ct);
        var zaznamInfo = await LoadZaznamInfoAsync(bufferItems, vyzvy, ct);

        var (muzeZalozit, duvod) = ZalozitPodminky(projekt);

        // Buffer je cíl vždy; z výzev jen rozpracované — do odeslané ani zrušené
        // se PNF přesunout nedá a nabízet neproveditelnou volbu nemá smysl (spec §8.3).
        var cilePresunu = new List<VyzvyCilPresunuViewModel>
        {
            new() { VyzvaId = null, Popisek = "Buffer" },
        };
        cilePresunu.AddRange(vyzvy
            .Where(v => v.Stav == VyzvaStav.Priprava)
            .Select(v => new VyzvyCilPresunuViewModel { VyzvaId = v.Id, Popisek = $"Výzva {v.Kod}" }));

        return new VyzvyPanelViewModel
        {
            ProjektId = projektId,
            MuzeEditovat = muzeEditovat,
            MuzeTisknout = muzeTisknout,
            VybranyRok = vybranyRok,
            DostupneRoky = dostupneRoky,
            Buffer = new VyzvyPanelBufferViewModel
            {
                Polozky = bufferItems.Select(b => ToPolozka(b, zaznamInfo)).ToArray(),
                Skupiny = Seskup(bufferItems.Select(b => ToPolozka(b, zaznamInfo)), zaznamInfo),
                MuzeZaloztVyzvu = muzeEditovat && muzeZalozit,
                DuvodBlokace = muzeZalozit ? null : duvod,
            },
            Vyzvy = vyzvy.Select(v => ToVyzva(v, jmenaOsob, zaznamInfo)).ToArray(),
            CilePresunu = cilePresunu,
        };
    }

    /// <summary>
    /// Co brání založení výzvy. Od 2026-09-07 už prázdný buffer nebrání — výzva vzniká
    /// prázdná a PNF se do ní přesouvají až potom (spec §5.2). Zbývají jen chybějící
    /// projektová data, ze kterých se berou snapshoty.
    /// </summary>
    private static (bool MuzeZalozit, string? Duvod) ZalozitPodminky(ProjektEntity? projekt)
    {
        if (projekt == null) return (false, "Projekt nenalezen.");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return (false, "Projekt nemá vyplněné Místo plnění. Doplňte v editaci projektu.");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return (false, "Projekt nemá vyplněné Číslo rámcové smlouvy. Doplňte v editaci projektu.");
        return (true, null);
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadOsobaNamesAsync(
        IReadOnlyList<VyzvaDetail> vyzvy, CancellationToken ct)
    {
        var ids = vyzvy.SelectMany(v => new[] { (int?)v.ZalozilOsobaId, v.OdeslalOsobaId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, string>();

        return await _db.Osoby.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => ((o.Titul ?? "") + " " + o.Jmeno + " " + o.Prijmeni).Trim(), ct);
    }

    /// <summary>Data záznamu potřebná pro popisek skupiny a pro její řazení.</summary>
    private sealed record ZaznamInfo(
        string? CisloViditelne, string Nazev, string? KategorieNazev,
        int CisloViditelneA, byte CisloViditelneTyp, int CisloViditelneB, int CisloZaznamu);

    private async Task<IReadOnlyDictionary<int, ZaznamInfo>> LoadZaznamInfoAsync(
        IReadOnlyList<VyzvaBufferItem> buffer, IReadOnlyList<VyzvaDetail> vyzvy, CancellationToken ct)
    {
        var zaznamIds = buffer.Select(b => b.ZaznamId)
            .Concat(vyzvy.SelectMany(v => v.Polozky.Select(p => p.ZaznamId)))
            .Distinct().ToArray();
        if (zaznamIds.Length == 0) return new Dictionary<int, ZaznamInfo>();

        return await (from z in _db.ProjektoveZaznamy.AsNoTracking()
                      join k in _db.CiselnikKategoriiZaznamu.AsNoTracking() on z.KategorieId equals k.Id into kj
                      from k in kj.DefaultIfEmpty()
                      where zaznamIds.Contains(z.Id)
                      select new
                      {
                          z.Id, z.CisloViditelne, z.Nazev,
                          KategorieNazev = k != null ? k.Nazev : null,
                          z.CisloViditelneA, z.CisloViditelneTyp, z.CisloViditelneB, z.CisloZaznamu,
                      })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ZaznamInfo(x.CisloViditelne, x.Nazev, x.KategorieNazev,
                                    x.CisloViditelneA, x.CisloViditelneTyp, x.CisloViditelneB, x.CisloZaznamu),
                ct);
    }

    /// <summary>
    /// Seskupí PNF podle záznamu. Skupiny se řadí sdílenou utilitou RecordDisplayOrdering —
    /// stejně jako záložka Záznamy a tisk, aby uživatel nepotkal tři různá pořadí.
    /// Uvnitř skupiny drží PNF stabilní pořadí podle Id externí vazby (pořadí přidání).
    /// </summary>
    private static IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Seskup(
        IEnumerable<VyzvyPanelPolozkaViewModel> polozky,
        IReadOnlyDictionary<int, ZaznamInfo> info)
        => polozky
            .GroupBy(p => p.ZaznamId)
            .Select(g =>
            {
                var i = info.GetValueOrDefault(g.Key);
                return new
                {
                    Skupina = new VyzvyPanelZaznamSkupinaViewModel
                    {
                        ZaznamId = g.Key,
                        CisloViditelne = i?.CisloViditelne,
                        Nazev = i?.Nazev,
                        Polozky = g.OrderBy(p => p.ExterniOdkazId).ToArray(),
                    },
                    Kategorie = RecordDisplayOrdering.CategoryOrder(i?.KategorieNazev),
                    PartA = RecordDisplayOrdering.VisibleNumberPartA(i?.CisloViditelneA ?? 0, i?.CisloZaznamu ?? 0),
                    PartB = RecordDisplayOrdering.VisibleNumberPartB(i?.CisloViditelneTyp ?? 0, i?.CisloViditelneB ?? 0),
                    Cislo = i?.CisloZaznamu ?? 0,
                };
            })
            .OrderBy(x => x.Kategorie).ThenBy(x => x.PartA).ThenBy(x => x.PartB).ThenBy(x => x.Cislo)
            .Select(x => x.Skupina)
            .ToArray();

    private static VyzvyPanelPolozkaViewModel ToPolozka(
        VyzvaBufferItem item, IReadOnlyDictionary<int, ZaznamInfo> zaznamInfo)
        => new()
        {
            ExterniOdkazId = item.ExterniOdkazId,
            ZaznamId = item.ZaznamId,
            Cislo = item.Cislo,
            StrucneNazev = item.StrucneNazev,
            PredpokladanaCena = item.PredpokladanaCena,
            KalkulaceCena = item.KalkulaceCena,
            CisloViditelneZaznamu = zaznamInfo.GetValueOrDefault(item.ZaznamId)?.CisloViditelne,
        };

    private static VyzvyPanelPolozkaViewModel ToPolozka(
        VyzvaDetailItem item, IReadOnlyDictionary<int, ZaznamInfo> zaznamInfo)
        => new()
        {
            ExterniOdkazId = item.ExterniOdkazId,
            ZaznamId = item.ZaznamId,
            Cislo = item.Cislo,
            StrucneNazev = item.StrucneNazev,
            PredpokladanaCena = item.PredpokladanaCena,
            KalkulaceCena = item.KalkulaceCena,
            CisloViditelneZaznamu = zaznamInfo.GetValueOrDefault(item.ZaznamId)?.CisloViditelne,
        };

    private static VyzvyPanelVyzvaViewModel ToVyzva(
        VyzvaDetail detail,
        IReadOnlyDictionary<int, string> jmenaOsob,
        IReadOnlyDictionary<int, ZaznamInfo> zaznamInfo)
    {
        var povoleneStavy = Enum.GetValues<VyzvaStav>()
            .Where(s => s != detail.Stav && VyzvaStateMachine.JePovolenyPrechod(detail.Stav, s))
            .Select(s => s.ToString())
            .ToArray();

        // Součet ze skutečných cen; kde kalkulace chybí, dopočte se předpokládaná
        // a počítá se zvlášť, aby smíšený součet nikdo nečetl jako konečný (R7).
        decimal? celkova = null;
        var jenPredpokladane = 0;
        foreach (var p in detail.Polozky)
        {
            var cena = p.KalkulaceCena ?? p.PredpokladanaCena;
            if (!cena.HasValue) continue;

            celkova = (celkova ?? 0) + cena.Value;
            if (!p.KalkulaceCena.HasValue) jenPredpokladane++;
        }

        return new VyzvyPanelVyzvaViewModel
        {
            Id = detail.Id,
            Kod = detail.Kod,
            PoradoveVRoce = detail.PoradoveVRoce,
            Rok = detail.Rok,
            Stav = detail.Stav.ToString(),
            DatumZalozeni = detail.DatumZalozeni,
            ZalozilJmeno = jmenaOsob.GetValueOrDefault(detail.ZalozilOsobaId),
            DatumOdeslani = detail.DatumOdeslani,
            OdeslalJmeno = detail.OdeslalOsobaId.HasValue
                ? jmenaOsob.GetValueOrDefault(detail.OdeslalOsobaId.Value)
                : null,
            Polozky = detail.Polozky.Select(p => ToPolozka(p, zaznamInfo)).ToArray(),
            Skupiny = Seskup(detail.Polozky.Select(p => ToPolozka(p, zaznamInfo)), zaznamInfo),
            CelkovaCena = celkova,
            PocetJenPredpokladanych = jenPredpokladane,
            PovoleneStavy = povoleneStavy,
            Kolapsovano = detail.Stav != VyzvaStav.Priprava,
        };
    }
}
