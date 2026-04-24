using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

public sealed class NesPanelExcelExportServiceTests
{
    private static ProjectDashboardNesPanelViewModel BuildModel(params NesPanelItemViewModel[] items)
        => new()
        {
            IsServiceDeskIntegrated = true,
            ServiceDeskInfoSystemId = 1,
            IsZkratka = "FIS",
            Items = items,
            PocetVProdleni = items.Length,
            PrumerneProdleniDni = items.Length == 0 ? 0 : items.Average(x => (double)x.DniProdleni)
        };

    [Fact]
    public void Build_PrazdnyModel_VraciValidniXlsx()
    {
        var service = new NesPanelExcelExportService();
        var model = BuildModel();

        var bytes = service.Build(model, new DateTime(2026, 4, 24, 12, 0, 0));

        bytes.Should().NotBeNullOrEmpty();
        // XLSX je ZIP — začíná PK magic (0x50 0x4B)
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }

    [Fact]
    public void Build_Items_ProduceExpectedRows()
    {
        var service = new NesPanelExcelExportService();
        var model = BuildModel(
            new NesPanelItemViewModel
            {
                TicketId = 111111, Pid = "A111111", TypZaznamu = "NES",
                Strucne = "Test issue", Dodavatel = "Acme",
                Termin = new DateTime(2026, 3, 1), DniProdleni = 10, Stav = "dodavatel",
                ServiceDeskUrl = "https://servicedesk.fis.acr/Hotline/Ticket/Details/111111"
            },
            new NesPanelItemViewModel
            {
                TicketId = 222222, Pid = "B222222", TypZaznamu = "PMP",
                Strucne = null, Dodavatel = null,
                Termin = new DateTime(2026, 2, 15), DniProdleni = 25, Stav = "otevřeno",
                ServiceDeskUrl = null
            });

        var bytes = service.Build(model, new DateTime(2026, 4, 24));

        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheetData = doc.WorkbookPart!.WorksheetParts.Single().Worksheet.Elements<SheetData>().Single();
        var rows = sheetData.Elements<Row>().ToList();
        // meta row + prázdná separator + header + 2 data rows = 5
        rows.Count.Should().BeGreaterThanOrEqualTo(5);

        // Najdi row s "Ticket ID" header
        var headerIndex = rows.FindIndex(r =>
            r.Elements<Cell>().Any(c => c.CellValue?.Text == "Ticket ID"));
        headerIndex.Should().BeGreaterThanOrEqualTo(0);

        var firstDataRow = rows[headerIndex + 1];
        var firstDataCells = firstDataRow.Elements<Cell>().ToList();
        firstDataCells[0].CellValue!.Text.Should().Be("111111");
        firstDataCells[2].CellValue!.Text.Should().Be("NES");
    }

    [Fact]
    public void Build_NullModel_Throws()
    {
        var service = new NesPanelExcelExportService();
        Action act = () => service.Build(null!, DateTime.UtcNow);
        act.Should().Throw<ArgumentNullException>();
    }
}
