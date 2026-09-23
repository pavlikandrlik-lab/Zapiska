using System.Linq;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Export;

namespace PmTracker.Tests.Unit.Export;

public sealed class OpenXmlWordExportServiceTests
{
    [Fact]
    public void BuildDocument_MeetingVariant_ShouldHideProjectRoles_AndRenderCommentAsColoredTextWithoutShading()
    {
        var sut = CreateSut();
        var model = new PdfExportTemplateViewModel
        {
            ExportVariant = "meeting",
            AutoPrint = false,
            ProjektId = 10,
            ProjektZkratka = "EXP",
            ProjektNazev = "Export projekt",
            JednaniId = 20,
            JednaniCislo = 551,
            JednaniDatum = new DateTime(2026, 2, 17),
            JednaniMisto = "A1",
            JednaniStav = "Otevřeno",
            Vytvoril = "Tester",
            VytvorenoDne = new DateTime(2026, 2, 18, 12, 0, 0),
            SnapshotSummary = string.Empty,
            PreparationSummary = null,
            ProjektoveRole =
            [
                new PdfRoleAssignmentViewModel
                {
                    Osoba = "Role Osoba",
                    TypRole = "Projektová",
                    Role = "Manažer",
                    Subsystem = null
                }
            ],
            AppliedRuleSummary = ["Automatický meeting výstup"],
            Legenda = [],
            Dochazka =
            [
                new PdfAttendanceGroupViewModel
                {
                    Stav = "Přítomen",
                    Osoby = ["Ing. Test Autor"]
                }
            ],
            Zaznamy =
            [
                new PdfExportRecordViewModel
                {
                    ZaznamId = 30,
                    CisloZaznamu = 1,
                    CisloViditelne = "1",
                    CisloViditelneA = 1,
                    CisloViditelneB = 0,
                    Nazev = "Test úkol",
                    Cil = null,
                    Popis = null,
                    KategorieKod = "U",
                    Kategorie = "Úkol",
                    TypUkoluKod = null,
                    TypUkolu = null,
                    Stav = "Otevřeno",
                    Vlastnik = "Ing. Test Autor",
                    SubsystemKod = "SUB",
                    Subsystem = "Subsystem",
                    DatumZalozeni = new DateTime(2026, 2, 1),
                    HistorieTerminu = [],
                    Termin = new DateTime(2026, 3, 1),
                    ExterniVazby = [],
                    Spoluprace = [],
                    Vyjadreni =
                    [
                        new PdfExportCommentViewModel
                        {
                            Autor = "Test Autor",
                            Datum = new DateTime(2026, 2, 18, 9, 0, 0),
                            Text = "<p>Komentář bez podkladu <a href=\"https://example.com\">odkaz</a></p>",
                            Delka = 26,
                            JednaniCislo = 551,
                            JednaniDatum = new DateTime(2026, 2, 17),
                            JednaniStav = "Otevřeno",
                            IsNew = true,
                            HighlightColor = "#2563EB"
                        }
                    ]
                }
            ]
        };

        var payload = sut.BuildDocument(model);

        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        body.Should().NotBeNull();

        var allText = string.Concat(body!.Descendants<Text>().Select(x => x.Text));
        allText.Should().NotContain("Role Osoba");
        allText.Should().Contain("Přítomen");
        allText.Should().Contain("Záznamy a vyjádření");

        const string expectedHeader = "jednání č. 551 (17.02.2026) | Test Autor | 18.02.2026";
        var commentHeaderParagraph = body.Descendants<Paragraph>()
            .FirstOrDefault(paragraph => paragraph.InnerText.Contains(expectedHeader, StringComparison.Ordinal));
        commentHeaderParagraph.Should().NotBeNull();
        commentHeaderParagraph!.ParagraphProperties?.GetFirstChild<Shading>().Should().BeNull();
        commentHeaderParagraph.Descendants<Run>()
            .Any(run => string.Equals(run.RunProperties?.Color?.Val?.Value, "2563EB", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();

        var commentBodyParagraph = body.Descendants<Paragraph>()
            .FirstOrDefault(paragraph => paragraph.InnerText.Contains("Komentář bez podkladu", StringComparison.Ordinal));
        commentBodyParagraph.Should().NotBeNull();
        commentBodyParagraph!.ParagraphProperties?.GetFirstChild<Shading>().Should().BeNull();
        commentBodyParagraph.Descendants<Run>()
            .Any(run => string.Equals(run.RunProperties?.Color?.Val?.Value, "2563EB", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();

        var hyperlinkRun = commentBodyParagraph.Descendants<Run>()
            .FirstOrDefault(run => run.InnerText.Contains("odkaz", StringComparison.Ordinal));
        hyperlinkRun.Should().NotBeNull();
        hyperlinkRun!.RunProperties?.Color?.Val?.Value.Should().Be("0563C1");
    }

    [Fact]
    public void BuildDocument_ShouldRenderOrderedAndBulletLists_AsSeparateLines()
    {
        var sut = CreateSut();
        var model = CreateModelWithCommentHtml("<ol><li>První</li><li>Druhý</li></ol><ul><li>Třetí</li></ul>");

        var payload = sut.BuildDocument(model);

        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        body.Should().NotBeNull();

        var paragraphTexts = body!.Descendants<Paragraph>()
            .Select(paragraph => paragraph.InnerText)
            .ToList();

        paragraphTexts.Should().Contain(text => text.Contains("1. První", StringComparison.Ordinal));
        paragraphTexts.Should().Contain(text => text.Contains("2. Druhý", StringComparison.Ordinal));
        paragraphTexts.Should().Contain(text => text.Contains("• Třetí", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildDocument_ShouldShadeWholePausedRecordRow()
    {
        var sut = CreateSut();
        var model = new PdfExportTemplateViewModel
        {
            ExportVariant = "meeting",
            AutoPrint = false,
            ProjektId = 10,
            ProjektZkratka = "EXP",
            ProjektNazev = "Export projekt",
            JednaniId = 20,
            JednaniCislo = 551,
            JednaniDatum = new DateTime(2026, 2, 17),
            JednaniMisto = "A1",
            JednaniStav = "Otevřeno",
            Vytvoril = "Tester",
            VytvorenoDne = new DateTime(2026, 2, 18, 12, 0, 0),
            SnapshotSummary = string.Empty,
            PreparationSummary = null,
            ProjektoveRole = [],
            AppliedRuleSummary = ["Automatický meeting výstup"],
            Legenda = [],
            Dochazka = [],
            Zaznamy =
            [
                new PdfExportRecordViewModel
                {
                    ZaznamId = 30,
                    CisloZaznamu = 1,
                    CisloViditelne = "1",
                    CisloViditelneA = 1,
                    CisloViditelneB = 0,
                    Nazev = "Pozastavený úkol",
                    Cil = "Cíl",
                    Popis = "<p>Popis</p>",
                    KategorieKod = "U",
                    Kategorie = "Úkol",
                    TypUkoluKod = null,
                    TypUkolu = null,
                    Stav = "Pozastaveno",
                    IsPaused = true,
                    Vlastnik = "Ing. Test Autor",
                    SubsystemKod = "SUB",
                    Subsystem = "Subsystem",
                    DatumZalozeni = new DateTime(2026, 2, 1),
                    HistorieTerminu = [],
                    Termin = new DateTime(2026, 3, 1),
                    ExterniVazby = [],
                    Spoluprace = [],
                    Vyjadreni = []
                }
            ]
        };

        var payload = sut.BuildDocument(model);

        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        body.Should().NotBeNull();

        var recordRow = body!.Descendants<TableRow>()
            .FirstOrDefault(row => row.InnerText.Contains("Pozastavený úkol", StringComparison.Ordinal));
        recordRow.Should().NotBeNull();

        var cells = recordRow!.Elements<TableCell>().ToList();
        cells.Should().HaveCount(3);
        foreach (var cell in cells)
        {
            cell.TableCellProperties.Should().NotBeNull();
            cell.TableCellProperties!.GetFirstChild<Shading>().Should().NotBeNull();
            cell.TableCellProperties.GetFirstChild<Shading>()!.Fill?.Value.Should().Be("FDF4E8");
        }
    }

    [Fact]
    public void BuildDocument_ProjectFilteredVariant_ShouldRenderSummaryAndAppliedRules()
    {
        var sut = CreateSut();
        var model = new PdfExportTemplateViewModel
        {
            ExportVariant = "project_filtered",
            AutoPrint = false,
            ProjektId = 10,
            ProjektZkratka = "EXP",
            ProjektNazev = "Export projekt",
            JednaniId = null,
            JednaniCislo = null,
            JednaniDatum = null,
            JednaniMisto = null,
            JednaniStav = "Projekt",
            Vytvoril = "Tester",
            VytvorenoDne = new DateTime(2026, 2, 18, 12, 0, 0),
            SnapshotSummary = "Tisk projektu s použitím aktivních filtrů.",
            PreparationSummary = null,
            ProjektoveRole = [],
            AppliedRuleSummary = ["Pouze aktivní úkoly", "Vlastník: Tester"],
            Legenda = [],
            Dochazka = [],
            Zaznamy = []
        };

        var payload = sut.BuildDocument(model);

        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        body.Should().NotBeNull();

        var allText = string.Concat(body!.Descendants<Text>().Select(x => x.Text));
        allText.Should().Contain("Souhrn");
        allText.Should().Contain("Tisk projektu s použitím aktivních filtrů.");
        allText.Should().Contain("Použitá pravidla");
        allText.Should().Contain("Pouze aktivní úkoly");
        allText.Should().Contain("Vlastník: Tester");
    }

    /// <summary>
    /// 2026-09-04: tištěné výstupy neměly číslování stránek. Word (.docx) ho umí nativně
    /// polem PAGE v zápatí — dole uprostřed.
    /// </summary>
    [Fact]
    public void BuildDocument_ShouldNumberPages_InCenteredFooter()
    {
        var sut = CreateSut();

        var payload = sut.BuildDocument(CreateModelWithCommentHtml("<p>Text</p>"));

        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var mainPart = document.MainDocumentPart!;

        var footerPart = mainPart.FooterParts.FirstOrDefault();
        footerPart.Should().NotBeNull("dokument musí mít zápatí s číslem stránky");

        var footerXml = footerPart!.Footer.OuterXml;
        footerXml.Should().Contain("PAGE", "číslo stránky se sází polem PAGE (přečísluje se samo)");
        footerXml.Should().Contain("NUMPAGES", "u čísla je i celkový počet stran");

        var justification = footerPart.Footer.Descendants<Justification>().FirstOrDefault();
        justification.Should().NotBeNull();
        justification!.Val!.Value.Should().Be(JustificationValues.Center, "číslování patří doprostřed");

        // Sekce musí zápatí odkazovat, jinak se v dokumentu nezobrazí.
        var sectionProperties = mainPart.Document.Body!.Elements<SectionProperties>().Single();
        sectionProperties.Elements<FooterReference>().Should().ContainSingle();
    }

    private static OpenXmlWordExportService CreateSut()
    {
        return new OpenXmlWordExportService(new RichTextContentService());
    }

    private static PdfExportTemplateViewModel CreateModelWithCommentHtml(string commentHtml)
    {
        return new PdfExportTemplateViewModel
        {
            ExportVariant = "meeting",
            AutoPrint = false,
            ProjektId = 10,
            ProjektZkratka = "EXP",
            ProjektNazev = "Export projekt",
            JednaniId = 20,
            JednaniCislo = 551,
            JednaniDatum = new DateTime(2026, 2, 17),
            JednaniMisto = "A1",
            JednaniStav = "Otevřeno",
            Vytvoril = "Tester",
            VytvorenoDne = new DateTime(2026, 2, 18, 12, 0, 0),
            SnapshotSummary = string.Empty,
            PreparationSummary = null,
            ProjektoveRole = [],
            AppliedRuleSummary = ["Automatický meeting výstup"],
            Legenda = [],
            Dochazka =
            [
                new PdfAttendanceGroupViewModel
                {
                    Stav = "Přítomen",
                    Osoby = ["Ing. Test Autor"]
                }
            ],
            Zaznamy =
            [
                new PdfExportRecordViewModel
                {
                    ZaznamId = 30,
                    CisloZaznamu = 1,
                    CisloViditelne = "1",
                    CisloViditelneA = 1,
                    CisloViditelneB = 0,
                    Nazev = "Test úkol",
                    Cil = null,
                    Popis = null,
                    KategorieKod = "U",
                    Kategorie = "Úkol",
                    TypUkoluKod = null,
                    TypUkolu = null,
                    Stav = "Otevřeno",
                    Vlastnik = "Ing. Test Autor",
                    SubsystemKod = "SUB",
                    Subsystem = "Subsystem",
                    DatumZalozeni = new DateTime(2026, 2, 1),
                    HistorieTerminu = [],
                    Termin = new DateTime(2026, 3, 1),
                    ExterniVazby = [],
                    Spoluprace = [],
                    Vyjadreni =
                    [
                        new PdfExportCommentViewModel
                        {
                            Autor = "Test Autor",
                            Datum = new DateTime(2026, 2, 18, 9, 0, 0),
                            Text = commentHtml,
                            Delka = 26,
                            JednaniCislo = 551,
                            JednaniDatum = new DateTime(2026, 2, 17),
                            JednaniStav = "Otevřeno",
                            IsNew = true,
                            HighlightColor = "#2563EB"
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>Podbarvení ukončených (2026-09-05): Word podbarví celý řádek jako u pozastavených.</summary>
    [Fact]
    public void BuildDocument_ShouldShadeWholeCompletedRecordRow()
    {
        var payload = CreateSut().BuildDocument(
            CreateModelWithSingleRecord("Ukončený úkol", isPaused: false, isCompleted: true));

        AssertRowShading(payload, "Ukončený úkol", "EFF6FF");
    }

    /// <summary>
    /// Podbarvení ukončených (2026-09-05), spec §4.3: kdyby data přinesla obojí,
    /// vyhrává pozastavení.
    /// </summary>
    [Fact]
    public void BuildDocument_ShouldPreferPausedShading_OverCompleted()
    {
        var payload = CreateSut().BuildDocument(
            CreateModelWithSingleRecord("Sporný úkol", isPaused: true, isCompleted: true));

        AssertRowShading(payload, "Sporný úkol", "FDF4E8");
    }

    private static PdfExportTemplateViewModel CreateModelWithSingleRecord(
        string recordName, bool isPaused, bool isCompleted) => new()
    {
        ExportVariant = "meeting",
        AutoPrint = false,
        ProjektId = 10,
        ProjektZkratka = "EXP",
        ProjektNazev = "Export projekt",
        JednaniId = 20,
        JednaniCislo = 551,
        JednaniDatum = new DateTime(2026, 2, 17),
        JednaniMisto = "A1",
        JednaniStav = "Otevřeno",
        Vytvoril = "Tester",
        VytvorenoDne = new DateTime(2026, 2, 18, 12, 0, 0),
        SnapshotSummary = string.Empty,
        PreparationSummary = null,
        ProjektoveRole = [],
        AppliedRuleSummary = ["Automatický meeting výstup"],
        Legenda = [],
        Dochazka = [],
        Zaznamy =
        [
            new PdfExportRecordViewModel
            {
                ZaznamId = 30,
                CisloZaznamu = 1,
                CisloViditelne = "1",
                CisloViditelneA = 1,
                CisloViditelneB = 0,
                Nazev = recordName,
                Cil = "Cíl",
                Popis = "<p>Popis</p>",
                KategorieKod = "U",
                Kategorie = "Úkol",
                TypUkoluKod = null,
                TypUkolu = null,
                Stav = isPaused ? "Pozastaveno" : "Ukončeno",
                IsPaused = isPaused,
                IsCompleted = isCompleted,
                Vlastnik = "Ing. Test Autor",
                SubsystemKod = "SUB",
                Subsystem = "Subsystem",
                DatumZalozeni = new DateTime(2026, 2, 1),
                HistorieTerminu = [],
                Termin = new DateTime(2026, 3, 1),
                ExterniVazby = [],
                Spoluprace = [],
                Vyjadreni = []
            }
        ]
    };

    private static void AssertRowShading(byte[] payload, string recordName, string expectedFillHex)
    {
        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        body.Should().NotBeNull();

        var recordRow = body!.Descendants<TableRow>()
            .FirstOrDefault(row => row.InnerText.Contains(recordName, StringComparison.Ordinal));
        recordRow.Should().NotBeNull();

        var cells = recordRow!.Elements<TableCell>().ToList();
        cells.Should().HaveCount(3);
        foreach (var cell in cells)
        {
            cell.TableCellProperties.Should().NotBeNull();
            cell.TableCellProperties!.GetFirstChild<Shading>().Should().NotBeNull();
            cell.TableCellProperties.GetFirstChild<Shading>()!.Fill?.Value.Should().Be(expectedFillHex);
        }
    }
}
