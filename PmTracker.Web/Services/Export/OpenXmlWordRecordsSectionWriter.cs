using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public sealed class OpenXmlWordRecordsSectionWriter(
    IWordExportRecordCommentsCellWriter commentsCellWriter,
    IWordExportRecordPeopleCellWriter peopleCellWriter,
    IWordExportRecordDeadlinesCellWriter deadlinesCellWriter) : IWordExportRecordsSectionWriter
{
    public void Append(Body body, MainDocumentPart mainPart, IReadOnlyList<PdfExportRecordViewModel> records)
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
                table.Append(CreateRecordRow(mainPart, record));
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
            new GridColumn { Width = "8100" },
            new GridColumn { Width = "2100" },
            new GridColumn { Width = "1600" }));

        return table;
    }

    private static TableRow CreateHeaderRow()
    {
        return new TableRow(
            OpenXmlWordElements.CreateCell("Záznamy a vyjádření", bold: true, fillColor: "F3F4F6"),
            OpenXmlWordElements.CreateCell("Osoby", bold: true, fillColor: "F3F4F6"),
            OpenXmlWordElements.CreateCell("Termíny", bold: true, fillColor: "F3F4F6"));
    }

    private static TableRow CreateSingleCellRow(string text, int gridSpan, string? fillColor = null, bool italic = false)
    {
        var cellProps = new TableCellProperties(new GridSpan { Val = gridSpan });
        if (!string.IsNullOrWhiteSpace(fillColor))
        {
            cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillColor, Color = "auto" });
        }

        var cell = new TableCell(cellProps);
        cell.Append(OpenXmlWordElements.CreateParagraph(text, bold: !italic, italic: italic, sizeHalfPoints: italic ? 19 : 22, before: 40, after: 40));
        return new TableRow(cell);
    }

    private TableRow CreateRecordRow(MainDocumentPart mainPart, PdfExportRecordViewModel record)
    {
        var commentsCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "8100" }));
        var peopleCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2100" }));
        var deadlinesCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "1600" }));

        commentsCellWriter.Append(commentsCell, mainPart, record);
        peopleCellWriter.Append(peopleCell, record);
        deadlinesCellWriter.Append(deadlinesCell, record);

        return new TableRow(commentsCell, peopleCell, deadlinesCell);
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
}
