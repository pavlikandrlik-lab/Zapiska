using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Vyzvy;

/// <summary>
/// Skládá výzvu pro tisk. Jediná projekce pro Word i PDF (spec 2026-09-07 §9.2).
/// </summary>
public interface IVyzvaExportBuilder
{
    /// <summary>Null, když výzva neexistuje. Prázdná výzva vrací model bez požadavků (§9.7).</summary>
    Task<VyzvaExportViewModel?> BuildAsync(int vyzvaId, CancellationToken ct);
}

/// <summary>
/// Projekce výzvy podle finálního vzoru (spec 2026-09-10 část B): římské pořadí, číslo úkolu
/// s prefixem „RU", tabulky podle toho, co kalkulace obsahuje, licence z rozpad_licence.
/// Vazba na PMP se do výzvy nedává (rozhodnutí uživatele 2026-09-10).
/// </summary>
public sealed class VyzvaExportBuilder : IVyzvaExportBuilder
{
    private const string PrefixUkolu = "RU";

    /// <summary>
    /// Řádky kalkulační tabulky. Pořadí, kódy i názvy jsou dané formulářem a odpovídají
    /// sloupcům HOT_KALKULACE (pracnost_a/p/t/i) — nejsou to data, je to struktura.
    /// </summary>
    private static readonly (string Kod, string Nazev)[] KalkulaceRadky =
    {
        ("A", "Analýza"),
        ("B", "Programové úpravy"),
        ("C", "Testování"),
        ("D", "Implementace"),
    };

    private static readonly (int Hodnota, string Znak)[] RimskeCislice =
    {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    };

    private readonly PmTrackerDbContext _db;
    private readonly ITicketingQueryService _ticketing;
    private readonly IRichTextContentService _richText;
    private readonly ILogger<VyzvaExportBuilder> _logger;

    public VyzvaExportBuilder(
        PmTrackerDbContext db,
        ITicketingQueryService ticketing,
        IRichTextContentService richText,
        ILogger<VyzvaExportBuilder>? logger = null)
    {
        _db = db;
        _ticketing = ticketing;
        _richText = richText;
        _logger = logger ?? NullLogger<VyzvaExportBuilder>.Instance;
    }

    public async Task<VyzvaExportViewModel?> BuildAsync(int vyzvaId, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null) return null;

        var mistoPlneniProjektu = await _db.Projekty.AsNoTracking()
            .Where(p => p.Id == vyzva.ProjektId)
            .Select(p => p.MistoPlneni)
            .FirstOrDefaultAsync(ct);
        var mistoPlneni = VyzvaMistoPlneni.Platne(vyzva.Stav, vyzva.MistoPlneniSnapshot, mistoPlneniProjektu);

        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId == vyzvaId)
            .Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.Pozadavek })
            .ToListAsync(ct);

        var zaznamIds = polozky.Select(x => x.ZaznamId).Distinct().ToArray();
        var zaznamy = await LoadZaznamyAsync(zaznamIds, ct);

        // HOT popisy nesou i PID, přes který teprve jdou dotáhnout kalkulace (§9.5).
        var hot = await _ticketing.GetZaznamyAsync(
            polozky.Select(x => x.Cislo).Distinct().ToArray(), ct);
        var pidy = hot.Values
            .Select(h => h.Pid)
            .Where(pid => !string.IsNullOrWhiteSpace(pid))
            .Select(pid => pid!)
            .Distinct()
            .ToArray();
        var kalkulace = await _ticketing.GetAkceptovaneKalkulaceAsync(pidy, ct);

        var poradi = 0;
        var pozadavky = polozky
            .OrderBy(x => SkupinoveRazeni(zaznamy.GetValueOrDefault(x.ZaznamId)))
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var z = zaznamy.GetValueOrDefault(x.ZaznamId);
                var h = hot.GetValueOrDefault(x.Cislo);
                var k = h?.Pid is string pid ? kalkulace.GetValueOrDefault(pid) : null;

                return new VyzvaExportPozadavekViewModel
                {
                    PoradoveOznaceni = PoradoveOznaceni(poradi++),
                    ZaznamId = x.ZaznamId,
                    CisloUkoluVp = CisloUkoluVp(z?.CisloViditelne),
                    Nazev = z?.Nazev,
                    CisloHtl = x.Cislo,
                    // Sanitizace i tady, ne jen při uložení: dokument je výstup ven
                    // a nesmí záviset na tom, že do DB nikdy nic nepřišlo jinudy.
                    PozadavekHtml = _richText.HasVisibleText(x.Pozadavek)
                        ? _richText.ToSafeHtml(x.Pozadavek)
                        : null,
                    Kalkulace = Kalkulace(k, x.Cislo),
                };
            })
            .ToArray();

        var celkemBez = pozadavky.Sum(p => p.Kalkulace.CelkemBezDph);
        var licenceBez = pozadavky.Sum(p => p.Kalkulace.CenaLicence ?? 0m);

        return new VyzvaExportViewModel
        {
            VyzvaId = vyzva.Id,
            ProjektId = vyzva.ProjektId,
            KodVyzvy = vyzva.Kod,
            PoradoveVRoce = vyzva.PoradoveVRoce,
            Rok = vyzva.Rok,
            CisloRamcoveSmlouvy = vyzva.CisloRamcoveSmlouvySnapshot,
            MistoPlneni = mistoPlneni,
            InformacniSystem = InformacniSystem(mistoPlneni),
            Pozadavky = pozadavky,
            CelkemBezDph = celkemBez,
            CelkemDph = Dph(celkemBez),
            CelkemSDph = celkemBez + Dph(celkemBez),
            LicenceBezDph = licenceBez,
            LicenceDph = Dph(licenceBez),
            LicenceSDph = licenceBez + Dph(licenceBez),
        };
    }

    private static decimal Dph(decimal bezDph)
        => decimal.Round(bezDph * VyzvaExportViewModel.DphSazba, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Zkratka IS z místa plnění: „FIS (EIS): VZ 8201" dá „FIS". Formulář ji používá
    /// v nadpisu výzvy.
    /// </summary>
    private static string InformacniSystem(string mistoPlneni)
    {
        var token = mistoPlneni.Split(new[] { ' ', '(', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
    }

    /// <summary>Poř. č. římsky s tečkou: „I.", „II.", … (komentář autora vzoru 2026-09-10).</summary>
    internal static string PoradoveOznaceni(int index)
    {
        var cislo = index + 1;
        var vysledek = new System.Text.StringBuilder();
        foreach (var (hodnota, znak) in RimskeCislice)
        {
            while (cislo >= hodnota)
            {
                vysledek.Append(znak);
                cislo -= hodnota;
            }
        }

        return vysledek.Append('.').ToString();
    }

    /// <summary>
    /// Č. úkolu VP s prefixem „RU" (komentář autora vzoru). CisloViditelne ho v produkci
    /// nenese — skládá se jako {jednání}-{pořadí} nebo {číslo}. Pojistka proti zdvojení kryje
    /// importovaná data i testový harness, který „RU" seeduje.
    /// </summary>
    internal static string? CisloUkoluVp(string? cisloViditelne)
    {
        if (string.IsNullOrWhiteSpace(cisloViditelne)) return null;

        var cislo = cisloViditelne.Trim();
        return cislo.StartsWith(PrefixUkolu, StringComparison.OrdinalIgnoreCase)
            ? cislo
            : PrefixUkolu + cislo;
    }

    private VyzvaExportKalkulaceViewModel Kalkulace(HotKalkulaceDto? k, string cisloPnf)
    {
        var hodnoty = new (decimal? Rozsah, decimal? Sazba, decimal? Cena)[]
        {
            (k?.PracnostAnalyza, k?.SazbaAnalyza, k?.CenaAnalyza),
            (k?.PracnostProgramovani, k?.SazbaProgramovani, k?.CenaProgramovani),
            (k?.PracnostTestovani, k?.SazbaTestovani, k?.CenaTestovani),
            (k?.PracnostImplementace, k?.SazbaImplementace, k?.CenaImplementace),
        };

        var radky = KalkulaceRadky.Select((r, i) =>
        {
            var (rozsah, sazba, cena) = hodnoty[i];
            var dph = cena.HasValue ? Dph(cena.Value) : (decimal?)null;
            return new VyzvaExportKalkulaceRadekViewModel
            {
                Kod = r.Kod,
                Nazev = r.Nazev,
                Rozsah = rozsah,
                Sazba = sazba,
                CenaBezDph = cena,
                CenaDph = dph,
                CenaSDph = cena.HasValue ? cena.Value + dph!.Value : null,
            };
        }).ToArray();

        var celkemBez = radky.Sum(r => r.CenaBezDph ?? 0m);
        var licence = LicenceRadky(k, cisloPnf);

        return new VyzvaExportKalkulaceViewModel
        {
            Radky = radky,
            CelkemBezDph = celkemBez,
            CelkemDph = Dph(celkemBez),
            CelkemSDph = celkemBez + Dph(celkemBez),
            MaCinnosti = celkemBez > 0m,
            MaLicenci = licence.Count > 0,
            LicenceRadky = licence,
            PocetLicenci = k?.PocetLicenci,
            SazbaLicence = k?.SazbaLicence,
            CenaLicence = licence.Count > 0 ? licence.Sum(r => r.CenaBezDph) : null,
        };
    }

    /// <summary>
    /// Řádky tabulky licencí (spec B5). Zdroj je rozpad_licence, cena_l je kontrolní součet —
    /// když se rozejdou, tiskne se rozpad a rozdíl jde do logu. Bez čitelného rozpadu jeden
    /// řádek „Licenční rozšíření" s cena_l. Bez licence prázdný seznam.
    /// </summary>
    private IReadOnlyList<VyzvaExportLicenceRadekViewModel> LicenceRadky(HotKalkulaceDto? k, string cisloPnf)
    {
        if (k?.CenaLicence is not decimal cenaL || cenaL <= 0m)
        {
            return Array.Empty<VyzvaExportLicenceRadekViewModel>();
        }

        IReadOnlyList<RozpadLicenceParser.Polozka> polozky = RozpadLicenceParser.Parse(k.RozpadLicence);
        if (polozky.Count == 0)
        {
            polozky = new[] { new RozpadLicenceParser.Polozka("Licenční rozšíření", cenaL) };
        }
        else if (polozky.Sum(p => p.Cena) != cenaL)
        {
            _logger.LogWarning(
                "Výzva: rozpad licence PNF {Cislo} dává {Soucet}, ale cena_l je {CenaL}. Tiskne se rozpad.",
                cisloPnf, polozky.Sum(p => p.Cena), cenaL);
        }

        return polozky.Select((p, i) => new VyzvaExportLicenceRadekViewModel
        {
            Kod = i + 1,
            Nazev = p.Nazev,
            CenaBezDph = p.Cena,
            CenaDph = Dph(p.Cena),
            CenaSDph = p.Cena + Dph(p.Cena),
        }).ToArray();
    }

    private sealed record ZaznamInfo(
        string? CisloViditelne, string Nazev, string? KategorieNazev,
        int CisloViditelneA, byte CisloViditelneTyp, int CisloViditelneB, int CisloZaznamu);

    /// <summary>
    /// Řazení požadavků drží sdílené RecordDisplayOrdering — stejné pořadí jako záložka
    /// Záznamy a panel Výzev, aby uživatel nepotkal třetí pořadí (spec §9.4).
    /// </summary>
    private static (int, int, int, int) SkupinoveRazeni(ZaznamInfo? i)
        => (RecordDisplayOrdering.CategoryOrder(i?.KategorieNazev),
            RecordDisplayOrdering.VisibleNumberPartA(i?.CisloViditelneA ?? 0, i?.CisloZaznamu ?? 0),
            RecordDisplayOrdering.VisibleNumberPartB(i?.CisloViditelneTyp ?? 0, i?.CisloViditelneB ?? 0),
            i?.CisloZaznamu ?? 0);

    private async Task<IReadOnlyDictionary<int, ZaznamInfo>> LoadZaznamyAsync(
        int[] zaznamIds, CancellationToken ct)
    {
        if (zaznamIds.Length == 0) return new Dictionary<int, ZaznamInfo>();

        return await (from z in _db.ProjektoveZaznamy.AsNoTracking()
                      join kat in _db.CiselnikKategoriiZaznamu.AsNoTracking() on z.KategorieId equals kat.Id into kj
                      from kat in kj.DefaultIfEmpty()
                      where zaznamIds.Contains(z.Id)
                      select new
                      {
                          z.Id, z.CisloViditelne, z.Nazev,
                          KategorieNazev = kat != null ? kat.Nazev : null,
                          z.CisloViditelneA, z.CisloViditelneTyp, z.CisloViditelneB, z.CisloZaznamu,
                      })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ZaznamInfo(x.CisloViditelne, x.Nazev, x.KategorieNazev,
                                    x.CisloViditelneA, x.CisloViditelneTyp, x.CisloViditelneB, x.CisloZaznamu),
                ct);
    }
}
