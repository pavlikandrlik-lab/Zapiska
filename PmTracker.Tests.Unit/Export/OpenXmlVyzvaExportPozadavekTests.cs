using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Word vysází text požadavku jako formátovaný rich text, ne jako syrové HTML
/// (spec 2026-09-08 §5.7). Velikost 12 bodů = 24 půlbodů; písmo Times New Roman
/// nastavuje OpenXmlWordElements.CreateRunProperties pro celý dokument.
/// </summary>
public sealed class OpenXmlVyzvaExportPozadavekTests
{
    private static WordprocessingDocument Otevrit(byte[] payload)
        => WordprocessingDocument.Open(new MemoryStream(payload, writable: false), false);

    private static VyzvaExportViewModel Model(string? pozadavekHtml) => new()
    {
        VyzvaId = 10,
        ProjektId = 1,
        KodVyzvy = "2/2026",
        PoradoveVRoce = 2,
        Rok = 2026,
        CisloRamcoveSmlouvy = "23106000271",
        MistoPlneni = "FIS (EIS): VZ 8201",
        InformacniSystem = "FIS",
        Pozadavky = new[]
        {
            new VyzvaExportPozadavekViewModel
            {
                PoradoveOznaceni = "a",
                ZaznamId = 100,
                CisloHtl = "336865",
                Nazev = "Nazev pozadavku",
                PozadavekHtml = pozadavekHtml,
                Kalkulace = new VyzvaExportKalkulaceViewModel(),
            }
        }
    };

    [Fact]
    public void Build_TucnyTextZustaneTucny_ANeniVDokumentuHtml()
    {
        var model = Model("<p>Chceme <strong>sestavu</strong>.</p>");

        using var doc = Otevrit(new OpenXmlVyzvaExportService().BuildDocument(model));
        var body = doc.MainDocumentPart!.Document.Body!;
        var text = body.InnerText;

        text.Should().Contain("Chceme").And.Contain("sestavu");
        text.Should().NotContain("<strong>", "HTML značky se do dokumentu nesmí dostat");

        body.Descendants<Run>()
            .Where(r => r.InnerText.Contains("sestavu"))
            .Should().Contain(r => r.RunProperties!.Bold != null,
                "tučné z editoru musí zůstat tučné i ve Wordu");
    }

    [Fact]
    public void Build_TextPozadavkuMa12Bodu()
    {
        var model = Model("<p>Chceme sestavu.</p>");

        using var doc = Otevrit(new OpenXmlVyzvaExportService().BuildDocument(model));

        doc.MainDocumentPart!.Document.Body!.Descendants<Run>()
            .Where(r => r.InnerText.Contains("Chceme"))
            .Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "24",
                "12 bodů = 24 půlbodů");
    }

    [Fact]
    public void Build_PrazdnyPozadavek_NicNevysazi()
    {
        var model = Model(null);

        using var doc = Otevrit(new OpenXmlVyzvaExportService().BuildDocument(model));

        doc.MainDocumentPart!.Document.Body!.InnerText
            .Should().NotContain("Chceme", "prázdná hodnota nemá co tisknout");
    }
}
