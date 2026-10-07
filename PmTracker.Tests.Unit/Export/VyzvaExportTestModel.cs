using DocumentFormat.OpenXml.Packaging;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Export;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Výzva pro testy Word exportu. Čtyři požadavky pokrývají všechny tvary kalkulace:
/// I. jen činnosti, II. jen licence, III. obojí, IV. bez akceptované kalkulace.
/// </summary>
internal static class VyzvaExportTestModel
{
    private static VyzvaExportKalkulaceRadekViewModel Radek(string kod, string nazev, decimal? cena) => new()
    {
        Kod = kod, Nazev = nazev,
        Rozsah = cena.HasValue ? 1m : null, Sazba = cena,
        CenaBezDph = cena, CenaDph = cena * 0.21m, CenaSDph = cena * 1.21m,
    };

    private static VyzvaExportKalkulaceRadekViewModel[] Radky(decimal? cena) => new[]
    {
        Radek("A", "Analýza", cena), Radek("B", "Programové úpravy", cena),
        Radek("C", "Testování", cena), Radek("D", "Implementace", cena),
    };

    private static VyzvaExportLicenceRadekViewModel[] Licence() => new[]
    {
        new VyzvaExportLicenceRadekViewModel
        {
            Kod = 1, Nazev = "XRG – RSS Rozhraní", CenaBezDph = 94470m, CenaDph = 19838.70m, CenaSDph = 114308.70m,
        },
    };

    private static VyzvaExportPozadavekViewModel Pozadavek(
        string poradi, string htl, bool cinnosti, bool licence, string? html = null) => new()
    {
        PoradoveOznaceni = poradi,
        ZaznamId = 1,
        CisloUkoluVp = "RU867-5",
        Nazev = $"Pozadavek {htl}",
        CisloHtl = htl,
        PozadavekHtml = html,
        Kalkulace = new VyzvaExportKalkulaceViewModel
        {
            Radky = Radky(cinnosti ? 100m : null),
            CelkemBezDph = cinnosti ? 400m : 0m,
            CelkemDph = cinnosti ? 84m : 0m,
            CelkemSDph = cinnosti ? 484m : 0m,
            MaCinnosti = cinnosti,
            MaLicenci = licence,
            LicenceRadky = licence ? Licence() : Array.Empty<VyzvaExportLicenceRadekViewModel>(),
            CenaLicence = licence ? 94470m : null,
        },
    };

    private static VyzvaExportViewModel Model(params VyzvaExportPozadavekViewModel[] pozadavky) => new()
    {
        VyzvaId = 10,
        ProjektId = 1,
        KodVyzvy = "8/2026",
        PoradoveVRoce = 8,
        Rok = 2026,
        CisloRamcoveSmlouvy = "23106000271",
        MistoPlneni = "FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6",
        InformacniSystem = "FIS",
        Pozadavky = pozadavky,
        CelkemBezDph = pozadavky.Sum(p => p.Kalkulace.CelkemBezDph),
        LicenceBezDph = pozadavky.Sum(p => p.Kalkulace.CenaLicence ?? 0m),
    };

    public static VyzvaExportViewModel VsechnyTvary(string? html = null) => Model(
        Pozadavek("I.", "358333", cinnosti: true, licence: false, html),
        Pozadavek("II.", "358310", cinnosti: false, licence: true),
        Pozadavek("III.", "361652", cinnosti: true, licence: true),
        Pozadavek("IV.", "364451", cinnosti: false, licence: false));

    public static WordprocessingDocument Otevrit(VyzvaExportViewModel model)
        => WordprocessingDocument.Open(
            new MemoryStream(new OpenXmlVyzvaExportService().BuildDocument(model), writable: false), false);
}
