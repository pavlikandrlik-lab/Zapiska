using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Plán 4 Feature C gap #4 (2026-04-24) — Excel export NES panelu projektového dashboardu.
/// Dashboard spec §107 zmínil Excel export, ale původní Sprint B plán to odložil.
/// Minimum viable OpenXML Spreadsheet writer: hlavička + data rows, bez stylů.
///
/// Output: XLSX byte[] pro kontroller na download.
/// </summary>
public interface INesPanelExcelExportService
{
    byte[] Build(ProjectDashboardNesPanelViewModel model, DateTime generatedAt);
}

public sealed class NesPanelExcelExportService : INesPanelExcelExportService
{
    private static readonly string[] Header =
    {
        "Ticket ID", "PID", "Typ", "Stručně", "Dodavatel",
        "Termín", "Dní prodlení", "Stav", "URL"
    };

    public byte[] Build(ProjectDashboardNesPanelViewModel model, DateTime generatedAt)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = $"NES {model.IsZkratka ?? "prodlení"}"
            });

            // Meta row (agregáty)
            sheetData.Append(BuildRow(new[]
            {
                "Vygenerováno", generatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("cs-CZ")),
                "IS", model.IsZkratka ?? "(bez napojení)",
                "V prodlení", model.PocetVProdleni.ToString(CultureInfo.InvariantCulture),
                "Průměr dní", model.PrumerneProdleniDni.ToString("F1", CultureInfo.InvariantCulture)
            }));

            // Empty separator row
            sheetData.Append(new Row());

            // Header row
            sheetData.Append(BuildRow(Header));

            // Data rows
            foreach (var item in model.Items)
            {
                sheetData.Append(BuildRow(new[]
                {
                    item.TicketId.ToString(CultureInfo.InvariantCulture),
                    item.Pid,
                    item.TypZaznamu,
                    item.Strucne ?? string.Empty,
                    item.Dodavatel ?? string.Empty,
                    item.Termin.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("cs-CZ")),
                    item.DniProdleni.ToString(CultureInfo.InvariantCulture),
                    item.Stav ?? string.Empty,
                    item.ServiceDeskUrl ?? string.Empty
                }));
            }

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Row BuildRow(IReadOnlyList<string> values)
    {
        var row = new Row();
        foreach (var value in values)
        {
            var cell = new Cell
            {
                DataType = CellValues.String,
                CellValue = new CellValue(value ?? string.Empty)
            };
            row.Append(cell);
        }
        return row;
    }
}
