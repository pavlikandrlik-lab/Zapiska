using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Excel záložky NES v prodlení (spec 2026-10-15 §10): list „V prodlení" s penalizovanými dny, typem dní,
/// odůvodněním (text + komentář) a daty penalizovaných dnů v komentáři; list „Nelze vyhodnotit".
/// </summary>
public sealed class NesPanelExcelExportServiceTests
{
    private static readonly DateTime Predano = new(2026, 10, 1, 9, 12, 0);
    private static readonly DateTime Vygenerovano = new(2026, 10, 20, 12, 0, 0);

    [Fact]
    public void Export_ma_sloupec_penalizovane_dny()
    {
        using var doc = Otevri(Model());

        var (hlavicka, data) = Tabulka(doc, "V prodlení");

        hlavicka.Should().Equal("Ticket", "Typ", "Důležitost", "Stručně", "Dodavatel", "U dodavatele od", "Termín",
            "Prodlouženo do", "Penalizované dny", "Typ dní", "Odůvodnění", "Odkaz ServiceDesk");
        data.Should().HaveCount(2);
        var prvni = data[0];
        prvni[0].Should().Be("900101");
        prvni[1].Should().Be("NES");
        prvni[2].Should().Be("B");
        prvni[5].Should().Be("01.10.2026 09:12");
        prvni[6].Should().Be("02.10.2026 09:12");
        prvni[7].Should().Be("07.10.2026 09:12");
        prvni[8].Should().Be("12");
        prvni[9].Should().Be("pracovní");
        prvni[11].Should().Be("https://servicedesk.fis.acr/Hotline/Ticket/Details/900101");
        data[1][9].Should().Be("kalendářní");
        data[1][7].Should().BeEmpty();
    }

    [Fact]
    public void Export_penalizovane_dny_jsou_cislo()
    {
        using var doc = Otevri(Model());

        var bunka = Bunky(doc, "V prodlení").Single(c => c.CellReference == "I5");

        bunka.DataType!.Value.Should().Be(CellValues.Number, "sloupec musí jít v Excelu sečíst");
        bunka.CellValue!.Text.Should().Be("12");
    }

    [Fact]
    public void Export_odůvodneni_v_komentari()
    {
        using var doc = Otevri(Model());

        var (_, data) = Tabulka(doc, "V prodlení");
        data[0][10].Should().Contain("penalizováno od termínu").And.Contain("dny do tohoto data včetně jsou prominuty");

        var komentare = Komentare(doc, "V prodlení");
        komentare.Should().ContainKey("K5");
        komentare["K5"].Should().Contain("penalizováno od termínu").And.Contain("\n", "každý důvod na vlastním řádku");
        komentare.Should().ContainKey("I5");
        komentare["I5"].Should().Contain("08.10.2026").And.Contain("09.10.2026");

        // Komentář bez VML tvaru Excel při otevření „opravuje" — tvar musí existovat pro každou poznámku.
        var list = List(doc, "V prodlení");
        list.VmlDrawingParts.Should().ContainSingle();
        using var vml = new StreamReader(list.VmlDrawingParts.Single().GetStream());
        var vmlText = vml.ReadToEnd();
        (vmlText.Split("<v:shape ").Length - 1).Should().Be(komentare.Count);
        list.Worksheet.Elements<LegacyDrawing>().Should().ContainSingle();
    }

    [Fact]
    public void Export_vice_dodavatelu_hvezdicka()
    {
        using var doc = Otevri(Model());

        var (_, data) = Tabulka(doc, "V prodlení");

        data[0][4].Should().Be("ACME");
        data[1][4].Should().Be("BETA *");
        VsechnyTexty(doc, "V prodlení").Should().Contain(t => t.StartsWith("* ") && t.Contains("mezi dodavateli"));
    }

    [Fact]
    public void Export_list_nelze_vyhodnotit()
    {
        using var doc = Otevri(Model());

        var (hlavicka, data) = Tabulka(doc, "Nelze vyhodnotit");

        hlavicka.Should().Equal("Ticket", "Typ", "Stručně", "Důležitost", "Důvod");
        data.Should().ContainSingle();
        data[0][0].Should().Be("900104");
        data[0][4].Should().Contain("SLA");
    }

    [Fact]
    public void Export_bez_prodleni_je_validni_sesit_bez_komentaru()
    {
        var model = new ProjectDashboardNesPanelViewModel
        {
            Prehled = new ProdleniPrehled(ProdleniStavDat.Ok, 1, "FIS", [], [], [], [], []),
            ProjektId = 1,
            Nyni = Vygenerovano,
        };

        using var doc = Otevri(model);

        Tabulka(doc, "V prodlení").Data.Should().BeEmpty();
        List(doc, "V prodlení").WorksheetCommentsPart.Should().BeNull();
        new OpenXmlValidator().Validate(doc).Should().BeEmpty();
    }

    [Fact]
    public void Export_s_daty_projde_validaci_openxml()
    {
        using var doc = Otevri(Model());

        new OpenXmlValidator().Validate(doc).Select(e => e.Description).Should().BeEmpty();
    }

    [Fact]
    public void Build_NullModel_Throws()
    {
        var service = new NesPanelExcelExportService();
        Action act = () => service.Build(null!, DateTime.UtcNow);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---- pomocné ----

    private static ProjectDashboardNesPanelViewModel Model()
    {
        IReadOnlyList<PenaltyReason> duvody =
        [
            new(PenaltyReasonKod.T05, Predano, null, Predano.AddDays(1), null, null, 12, null, null),
            new(PenaltyReasonKod.T14, null, null, null, null, Predano.AddDays(6), null, null, null),
        ];
        var prehled = new ProdleniPrehled(ProdleniStavDat.Ok, 1, "FIS",
            VProdleni:
            [
                new(900101, "FIS-900101", "NES", "B", "Chyba výpočtu", "ACME", false, Predano, Predano.AddDays(1),
                    Predano.AddDays(6), 12, [Predano.Date.AddDays(7), Predano.Date.AddDays(8)], true, [], duvody),
                new(900102, "FIS-900102", "PMP", null, null, "BETA", true, Predano, Predano.AddDays(2),
                    null, 5, [Predano.Date.AddDays(3)], false, [], duvody),
            ],
            Hrozi: [],
            NelzeVyhodnotit: [new(900104, "NES", "Bez SLA", "X", PenaltyChyba.SlaChybiProDulezitost)],
            Otevrene: [], Penalizace: []);
        return new ProjectDashboardNesPanelViewModel { Prehled = prehled, ProjektId = 1, Nyni = Vygenerovano };
    }

    private static SpreadsheetDocument Otevri(ProjectDashboardNesPanelViewModel model)
    {
        var bytes = new NesPanelExcelExportService().Build(model, Vygenerovano);
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
        return SpreadsheetDocument.Open(new MemoryStream(bytes), false);
    }

    private static WorksheetPart List(SpreadsheetDocument doc, string nazev)
    {
        var sheet = doc.WorkbookPart!.Workbook.Descendants<Sheet>().Single(s => s.Name == nazev);
        return (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id!);
    }

    private static List<Cell> Bunky(SpreadsheetDocument doc, string list)
        => List(doc, list).Worksheet.Descendants<Cell>().ToList();

    private static List<string> VsechnyTexty(SpreadsheetDocument doc, string list)
        => Bunky(doc, list).Select(c => c.CellValue?.Text ?? string.Empty).ToList();

    /// <summary>Hlavička = řádek začínající „Ticket"; data = řádky pod ní až po první prázdný.</summary>
    private static (List<string> Hlavicka, List<List<string>> Data) Tabulka(SpreadsheetDocument doc, string list)
    {
        var radky = List(doc, list).Worksheet.Descendants<Row>()
            .Select(r => r.Elements<Cell>().Select(c => c.CellValue?.Text ?? string.Empty).ToList())
            .ToList();
        var index = radky.FindIndex(r => r.Count > 0 && r[0] == "Ticket");
        index.Should().BeGreaterThanOrEqualTo(0, $"list {list} má hlavičku");
        var data = radky.Skip(index + 1).TakeWhile(r => r.Count > 0).ToList();
        return (radky[index], data);
    }

    private static Dictionary<string, string> Komentare(SpreadsheetDocument doc, string list)
        => List(doc, list).WorksheetCommentsPart!.Comments.Descendants<Comment>()
            .ToDictionary(c => c.Reference!.Value!, c => c.InnerText);
}
