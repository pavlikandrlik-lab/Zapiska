using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public sealed partial class OpenXmlWordExportService : IWordExportService
{
    private const int BaseFontHalfPoints = 20; // 10 pt
    private const int RecordTitleHalfPoints = 24; // 12 pt

    public byte[] BuildDocument(PdfExportTemplateViewModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body ?? throw new InvalidOperationException("Word body nebyl inicializován.");

            AppendHeaderTable(body, model);

            AppendRecordsTable(body, model.Zaznamy);
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string BuildDocumentTitle(PdfExportTemplateViewModel model)
    {
        var variant = (model.ExportVariant ?? "project_all").Trim().ToLowerInvariant();
        return variant switch
        {
            "meeting" => $"Zápis z jednání projektu {model.ProjektNazev} číslo {model.JednaniCislo}",
            "task_single" => $"Zápis úkolu projektu {model.ProjektNazev}",
            _ => $"Souhrnný zápis projektu {model.ProjektNazev}"
        };
    }

    private static void AppendHeaderTable(Body body, PdfExportTemplateViewModel model)
    {
        var variant = (model.ExportVariant ?? "project_all").Trim().ToLowerInvariant();
        var isMeeting = string.Equals(variant, "meeting", StringComparison.OrdinalIgnoreCase);
        var table = CreateHeaderTableSkeleton();
        table.Append(CreateHeaderTitleRow(BuildDocumentTitle(model)));
        table.Append(CreateHeaderKeyValueRow("Projekt", $"{model.ProjektNazev} ({model.ProjektZkratka})"));

        if (isMeeting)
        {
            table.Append(CreateHeaderKeyValueRow("Jednání", $"č. {model.JednaniCislo}"));
            table.Append(CreateHeaderKeyValueRow("Datum", model.JednaniDatum?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "-"));
            table.Append(CreateHeaderKeyValueRow("Stav", model.JednaniStav));
            table.Append(CreateHeaderKeyValueRow("Místo", string.IsNullOrWhiteSpace(model.JednaniMisto) ? "-" : model.JednaniMisto));
        }
        else
        {
            table.Append(CreateHeaderKeyValueRow("Typ výstupu", variant == "task_single" ? "Jeden úkol" : "Kompletní projekt"));
        }

        table.Append(CreateHeaderKeyValueRow("Generoval", model.Vytvoril));
        table.Append(CreateHeaderKeyValueRow("Vytvořeno", model.VytvorenoDne.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)));

        if (model.ProjektoveRole.Count > 0)
        {
            var roleLines = model.ProjektoveRole
                .Select(member =>
                {
                    var subsystemSuffix = string.IsNullOrWhiteSpace(member.Subsystem)
                        ? string.Empty
                        : $" ({member.Subsystem})";
                    return $"{member.Osoba} — {member.TypRole}: {member.Role}{subsystemSuffix}";
                })
                .ToList();
            table.Append(CreateHeaderMultilineRow("Projektové role", roleLines));
        }

        if (model.Dochazka.Count > 0)
        {
            foreach (var group in model.Dochazka)
            {
                var value = group.Osoby.Count == 0 ? "-" : string.Join(", ", group.Osoby);
                table.Append(CreateHeaderKeyValueRow(group.Stav, value, true));
            }
        }

        body.Append(table);
        body.Append(CreateSpacerParagraph(80));
    }

    private static Table CreateHeaderTableSkeleton()
    {
        var table = new Table();
        table.Append(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new BottomBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new LeftBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new RightBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideHorizontalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideVerticalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed }));

        table.Append(new TableGrid(
            new GridColumn { Width = "2600" },
            new GridColumn { Width = "7400" }));

        return table;
    }

    private static TableRow CreateHeaderTitleRow(string title)
    {
        var row = new TableRow();
        var cellProperties = new TableCellProperties(
            new GridSpan { Val = 2 },
            new Shading { Val = ShadingPatternValues.Clear, Fill = "D7ECFB", Color = "auto" });

        var cell = new TableCell(cellProperties);
        cell.Append(CreateParagraph(
            title,
            bold: true,
            sizeHalfPoints: 30,
            justification: JustificationValues.Center,
            before: 80,
            after: 80));

        row.Append(cell);
        return row;
    }

    private static TableRow CreateHeaderKeyValueRow(string label, string value, bool emphasizeLabel = true)
    {
        var row = new TableRow();
        row.Append(CreateCell(label, bold: emphasizeLabel));
        row.Append(CreateCell(value, bold: false));
        return row;
    }

    private static TableRow CreateHeaderMultilineRow(string label, IReadOnlyList<string> lines)
    {
        var row = new TableRow();
        row.Append(CreateCell(label, bold: true));

        var valueCell = new TableCell(new TableCellProperties());
        if (lines.Count == 0)
        {
            valueCell.Append(CreateParagraph("-", bold: false, sizeHalfPoints: BaseFontHalfPoints, before: 30, after: 30));
        }
        else
        {
            foreach (var line in lines)
            {
                valueCell.Append(CreateParagraph(line, bold: false, sizeHalfPoints: BaseFontHalfPoints, before: 20, after: 20));
            }
        }

        row.Append(valueCell);
        return row;
    }

    private static void AppendRecordsTable(Body body, IReadOnlyList<PdfExportRecordViewModel> records)
    {
        var table = CreateRecordsTableSkeleton();
        table.Append(CreateHeaderRow());

        if (records.Count == 0)
        {
            table.Append(CreateSingleCellRow("Žádná data k tisku.", gridSpan: 3, fillColor: null, italic: true));
            body.Append(table);
            return;
        }

        var subsystemGroups = records
            .GroupBy(x => x.Subsystem)
            .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var subsystemGroup in subsystemGroups)
        {
            table.Append(CreateSingleCellRow($"Subsystém: {subsystemGroup.Key}", gridSpan: 3, fillColor: "EAF2FA"));

            var orderedRecords = subsystemGroup
                .OrderBy(record => CategoryOrder(record.Kategorie))
                .ThenBy(record => record.Kategorie, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(record => record.CisloViditelneA)
                .ThenBy(record => record.CisloViditelneB)
                .ThenBy(record => record.CisloZaznamu)
                .ToList();

            foreach (var record in orderedRecords)
            {
                table.Append(CreateRecordRow(record));
            }
        }

        body.Append(table);
    }

    private static Table CreateRecordsTableSkeleton()
    {
        var table = new Table();
        table.Append(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new BottomBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new LeftBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new RightBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideHorizontalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U },
                new InsideVerticalBorder { Val = BorderValues.Single, Color = "1F2937", Size = 8U }),
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed }));

        table.Append(new TableGrid(
            new GridColumn { Width = "7600" },
            new GridColumn { Width = "2100" },
            new GridColumn { Width = "2100" }));

        return table;
    }

    private static TableRow CreateHeaderRow()
    {
        return new TableRow(
            CreateCell("Vyjádření", bold: true, fillColor: "F3F4F6"),
            CreateCell("Osoby", bold: true, fillColor: "F3F4F6"),
            CreateCell("Termíny", bold: true, fillColor: "F3F4F6"));
    }

    private static TableRow CreateSingleCellRow(string text, int gridSpan, string? fillColor = null, bool italic = false)
    {
        var cellProps = new TableCellProperties(new GridSpan { Val = gridSpan });
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(cellProps);
        cell.Append(CreateParagraph(text, bold: !italic, italic: italic, sizeHalfPoints: italic ? 19 : 22, before: 40, after: 40));
        return new TableRow(cell);
    }

    private static TableRow CreateRecordRow(PdfExportRecordViewModel record)
    {
        var commentsCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "7600" }));
        var peopleCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2100" }));
        var deadlinesCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2100" }));

        AppendRecordCommentsCell(commentsCell, record);
        AppendRecordPeopleCell(peopleCell, record);
        AppendRecordDeadlinesCell(deadlinesCell, record);

        return new TableRow(commentsCell, peopleCell, deadlinesCell);
    }

    private static void AppendRecordCommentsCell(TableCell cell, PdfExportRecordViewModel record)
    {
        var code = string.IsNullOrWhiteSpace(record.TypUkoluKod)
            ? (string.IsNullOrWhiteSpace(record.KategorieKod) ? "-" : record.KategorieKod)
            : record.TypUkoluKod;
        var isPaused = (record.Stav ?? string.Empty).Contains("pozastav", StringComparison.CurrentCultureIgnoreCase);
        var pausedFill = isPaused ? "F6D0B0" : null;

        cell.Append(CreateParagraph(
            $"{code}{record.CisloViditelne} - {record.Nazev}",
            bold: true,
            sizeHalfPoints: RecordTitleHalfPoints,
            before: 40,
            after: 40,
            shadingHex: pausedFill));

        cell.Append(CreateRichParagraph(
                new[]
                {
                    new TextSegment("Stav: ", Bold: true),
                    new TextSegment(string.IsNullOrWhiteSpace(record.Stav) ? "-" : record.Stav),
                    new TextSegment(" | "),
                    new TextSegment("Typ úkolu: ", Bold: true),
                    new TextSegment(string.IsNullOrWhiteSpace(record.TypUkolu) ? "-" : record.TypUkolu!)
                },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 0,
            after: 25));

        if (!string.IsNullOrWhiteSpace(record.Popis))
        {
            cell.Append(CreateRichParagraph(
                new[]
                {
                    new TextSegment("Popis: ", Bold: true),
                    new TextSegment(record.Popis)
                },
                sizeHalfPoints: BaseFontHalfPoints,
                before: 0,
                after: 25));
        }

        if (record.ExterniVazby.Count > 0)
        {
            cell.Append(CreateRichParagraph(
                new[]
                {
                    new TextSegment("Externí vazby: ", Bold: true),
                    new TextSegment(string.Join("; ", record.ExterniVazby))
                },
                sizeHalfPoints: BaseFontHalfPoints,
                before: 0,
                after: 25));
        }

        foreach (var comment in record.Vyjadreni)
        {
            var meetingRef = comment.JednaniCislo.HasValue ? $" | jednání č. {comment.JednaniCislo}" : string.Empty;
            var titleText = $"{comment.Datum:dd.MM.yyyy}{meetingRef} | {comment.Autor}";
            var fillColor = NormalizeHexColor(comment.HighlightColor);
            cell.Append(CreateParagraph(
                titleText,
                bold: true,
                sizeHalfPoints: BaseFontHalfPoints,
                before: 20,
                after: 0,
                shadingHex: fillColor));
            cell.Append(CreateParagraph(
                comment.Text,
                sizeHalfPoints: BaseFontHalfPoints,
                before: 0,
                after: 20,
                shadingHex: fillColor,
                preserveLineBreaks: true));
        }
    }

    private static void AppendRecordPeopleCell(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(CreateRichParagraph(
            new[]
            {
                new TextSegment("Vlastník: ", Bold: true),
                new TextSegment(record.Vlastnik)
            },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 40,
            after: 30));

        if (record.Spoluprace.Count == 0)
        {
            return;
        }

        cell.Append(CreateParagraph("Spolupráce:", bold: true, sizeHalfPoints: BaseFontHalfPoints, before: 20, after: 20));
        foreach (var person in record.Spoluprace)
        {
            cell.Append(CreateParagraph(person, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: 20));
        }
    }

    private static void AppendRecordDeadlinesCell(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(CreateRichParagraph(
            new[]
            {
                new TextSegment("Založeno: ", Bold: true),
                new TextSegment(record.DatumZalozeni.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
            },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 40,
            after: 20));

        cell.Append(CreateRichParagraph(
            new[]
            {
                new TextSegment("Aktuální termín: ", Bold: true),
                new TextSegment(record.Termin.HasValue ? record.Termin.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "-")
            },
            sizeHalfPoints: BaseFontHalfPoints,
            before: 0,
            after: 20));

        if (record.HistorieTerminu.Count == 0)
        {
            return;
        }

        cell.Append(CreateParagraph("Historie termínů:", bold: true, sizeHalfPoints: BaseFontHalfPoints, before: 20, after: 20));
        foreach (var date in record.HistorieTerminu.OrderByDescending(x => x))
        {
            cell.Append(CreateParagraph(date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), strike: true, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: 20));
        }
    }

    private static TableCell CreateCell(string text, bool bold = false, string? fillColor = null)
    {
        var properties = new TableCellProperties();
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(properties);
        cell.Append(CreateParagraph(text, bold: bold, sizeHalfPoints: BaseFontHalfPoints, before: 30, after: 30));
        return cell;
    }

    private static Paragraph CreateRichParagraph(
        IReadOnlyList<TextSegment> segments,
        int sizeHalfPoints = BaseFontHalfPoints,
        JustificationValues? justification = null,
        string? shadingHex = null,
        int before = 0,
        int after = 0)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (justification.HasValue)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        var paragraph = new Paragraph(paragraphProperties);
        foreach (var segment in segments)
        {
            var runProperties = CreateRunProperties(sizeHalfPoints, segment.Bold, segment.Italic, segment.Strike);
            paragraph.Append(new Run(runProperties, new Text(segment.Text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }));
        }

        return paragraph;
    }

    private static RunProperties CreateRunProperties(int sizeHalfPoints, bool bold, bool italic, bool strike)
    {
        var runProperties = new RunProperties(
            new RunFonts
            {
                Ascii = "Times New Roman",
                HighAnsi = "Times New Roman",
                EastAsia = "Times New Roman",
                ComplexScript = "Times New Roman"
            },
            new FontSize { Val = sizeHalfPoints.ToString(CultureInfo.InvariantCulture) });

        if (bold)
        {
            runProperties.Append(new Bold());
        }

        if (italic)
        {
            runProperties.Append(new Italic());
        }

        if (strike)
        {
            runProperties.Append(new Strike());
        }

        return runProperties;
    }

    private static Paragraph CreateParagraph(
        string text,
        bool bold = false,
        bool italic = false,
        bool strike = false,
        int sizeHalfPoints = BaseFontHalfPoints,
        JustificationValues? justification = null,
        string? shadingHex = null,
        int before = 0,
        int after = 0,
        bool preserveLineBreaks = false)
    {
        var paragraphProperties = new ParagraphProperties(
            new SpacingBetweenLines
            {
                Before = before == 0 ? null : before.ToString(CultureInfo.InvariantCulture),
                After = after == 0 ? null : after.ToString(CultureInfo.InvariantCulture)
            });

        if (justification.HasValue)
        {
            paragraphProperties.Append(new Justification { Val = justification.Value });
        }

        if (!string.IsNullOrWhiteSpace(shadingHex))
        {
            paragraphProperties.Append(new Shading
            {
                Val = ShadingPatternValues.Clear,
                Fill = shadingHex,
                Color = "auto"
            });
        }

        var paragraph = new Paragraph(paragraphProperties);
        var runProperties = CreateRunProperties(sizeHalfPoints, bold, italic, strike);
        var normalizedText = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        if (!preserveLineBreaks || normalizedText.Length == 0)
        {
            paragraph.Append(new Run(runProperties, new Text(normalizedText) { Space = SpaceProcessingModeValues.Preserve }));
            return paragraph;
        }

        var lines = normalizedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineRun = new Run(runProperties.CloneNode(true), new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.Append(lineRun);
            if (i < lines.Length - 1)
            {
                paragraph.Append(new Run(runProperties.CloneNode(true), new Break()));
            }
        }

        return paragraph;
    }

    private static Paragraph CreateSpacerParagraph(int after)
    {
        return CreateParagraph(string.Empty, sizeHalfPoints: BaseFontHalfPoints, before: 0, after: after);
    }

    private static int CategoryOrder(string? category)
    {
        var normalized = (category ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("info", StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalized.Contains("rozh", StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalized.Contains("ukol", StringComparison.Ordinal) || normalized.Contains("úkol", StringComparison.Ordinal))
        {
            return 3;
        }

        return 4;
    }

    private static string? NormalizeHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('#') && normalized.Length == 7)
        {
            return normalized[1..].ToUpperInvariant();
        }

        var match = RgbRegex().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        var red = ClampColor(match.Groups["r"].Value);
        var green = ClampColor(match.Groups["g"].Value);
        var blue = ClampColor(match.Groups["b"].Value);
        return $"{red:X2}{green:X2}{blue:X2}";
    }

    private static int ClampColor(string raw)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        return Math.Clamp(value, 0, 255);
    }

    [GeneratedRegex(@"^rgba?\(\s*(?<r>\d{1,3})\s*,\s*(?<g>\d{1,3})\s*,\s*(?<b>\d{1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RgbRegex();

    private readonly record struct TextSegment(
        string Text,
        bool Bold = false,
        bool Italic = false,
        bool Strike = false);
}
