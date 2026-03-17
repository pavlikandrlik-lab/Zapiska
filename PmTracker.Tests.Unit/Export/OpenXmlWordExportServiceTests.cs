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
                            HighlightColor = "#0F4D8A"
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
            .Any(run => string.Equals(run.RunProperties?.Color?.Val?.Value, "0F4D8A", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue();

        var commentBodyParagraph = body.Descendants<Paragraph>()
            .FirstOrDefault(paragraph => paragraph.InnerText.Contains("Komentář bez podkladu", StringComparison.Ordinal));
        commentBodyParagraph.Should().NotBeNull();
        commentBodyParagraph!.ParagraphProperties?.GetFirstChild<Shading>().Should().BeNull();
        commentBodyParagraph.Descendants<Run>()
            .Any(run => string.Equals(run.RunProperties?.Color?.Val?.Value, "0F4D8A", StringComparison.OrdinalIgnoreCase))
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

    private static OpenXmlWordExportService CreateSut()
    {
        var richText = new RichTextContentService();
        var richHtmlParagraphWriter = new OpenXmlWordRichHtmlParagraphWriter();
        var recordHeaderWriter = new OpenXmlWordRecordHeaderWriter(richText, richHtmlParagraphWriter);
        var commentsCellWriter = new OpenXmlWordRecordCommentsCellWriter(
            richText,
            richHtmlParagraphWriter,
            recordHeaderWriter);
        var peopleCellWriter = new OpenXmlWordRecordPeopleCellWriter();
        var deadlinesCellWriter = new OpenXmlWordRecordDeadlinesCellWriter();

        return new OpenXmlWordExportService(
            new OpenXmlWordHeaderSectionWriter(),
            new OpenXmlWordRecordsSectionWriter(
                commentsCellWriter,
                peopleCellWriter,
                deadlinesCellWriter));
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
                            HighlightColor = "#0F4D8A"
                        }
                    ]
                }
            ]
        };
    }
}
