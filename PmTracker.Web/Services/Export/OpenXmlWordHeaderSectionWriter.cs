using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public sealed class OpenXmlWordHeaderSectionWriter : IWordExportHeaderSectionWriter
{
    public void Append(Body body, PdfExportTemplateViewModel model, string documentTitle)
    {
        var variant = (model.ExportVariant ?? "project_all").Trim().ToLowerInvariant();
        var isMeeting = string.Equals(variant, "meeting", StringComparison.OrdinalIgnoreCase);
        var table = CreateHeaderTableSkeleton();
        table.Append(CreateHeaderTitleRow(documentTitle));
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

        if (!isMeeting && model.ProjektoveRole.Count > 0)
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
        body.Append(OpenXmlWordElements.CreateSpacerParagraph(80));
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
        cell.Append(OpenXmlWordElements.CreateParagraph(
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
        row.Append(OpenXmlWordElements.CreateCell(label, bold: emphasizeLabel));
        row.Append(OpenXmlWordElements.CreateCell(value, bold: false));
        return row;
    }

    private static TableRow CreateHeaderMultilineRow(string label, IReadOnlyList<string> lines)
    {
        var row = new TableRow();
        row.Append(OpenXmlWordElements.CreateCell(label, bold: true));

        var valueCell = new TableCell(new TableCellProperties());
        if (lines.Count == 0)
        {
            valueCell.Append(OpenXmlWordElements.CreateParagraph("-", bold: false, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 30, after: 30));
        }
        else
        {
            foreach (var line in lines)
            {
                valueCell.Append(OpenXmlWordElements.CreateParagraph(line, bold: false, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 20, after: 20));
            }
        }

        row.Append(valueCell);
        return row;
    }
}
