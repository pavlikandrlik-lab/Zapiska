using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Excel záložky NES v prodlení (spec 2026-10-15 §10). List „V prodlení": řádek na ticket a dodavatele
/// s penalizovanými dny, typem dní a odůvodněním; odůvodnění a data penalizovaných dnů jsou i v komentářích
/// buněk (stejně jako Excel starého ServiceDesku). List „Nelze vyhodnotit": tickety s chybou dat.
/// </summary>
public interface INesPanelExcelExportService
{
    byte[] Build(ProjectDashboardNesPanelViewModel model, DateTime generatedAt);
}

public sealed class NesPanelExcelExportService : INesPanelExcelExportService
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    private static readonly string[] HlavickaVProdleni =
    {
        "Ticket", "Typ", "Důležitost", "Stručně", "Dodavatel", "U dodavatele od", "Termín",
        "Prodlouženo do", "Penalizované dny", "Typ dní", "Odůvodnění", "Odkaz ServiceDesk"
    };

    private static readonly string[] HlavickaNelze = { "Ticket", "Typ", "Stručně", "Důležitost", "Důvod" };

    private const int SloupecDny = 8;         // I
    private const int SloupecOduvodneni = 10; // K

    public byte[] Build(ProjectDashboardNesPanelViewModel model, DateTime generatedAt)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());

            PridejList(workbookPart, sheets, 1, "V prodlení", list => NaplnVProdleni(list, model, generatedAt));
            PridejList(workbookPart, sheets, 2, "Nelze vyhodnotit", list => NaplnNelze(list, model));

            workbookPart.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static void NaplnVProdleni(ListBuilder list, ProjectDashboardNesPanelViewModel model, DateTime generatedAt)
    {
        var prehled = model.Prehled;

        list.Radek(
            "Vygenerováno", generatedAt.ToString("dd.MM.yyyy HH:mm", Cs),
            "IS", prehled.IsZkratka ?? "(bez napojení)",
            "Tickety v prodlení", model.PocetTicketuVProdleni.ToString(CultureInfo.InvariantCulture),
            "Penalizovaných dnů", prehled.SoucetDni.ToString(CultureInfo.InvariantCulture));
        list.Radek("* Ticket přešel mezi dodavateli, penále se počítá pro každého dodavatele zvlášť.");
        list.PrazdnyRadek();
        list.Radek(HlavickaVProdleni);

        foreach (var p in prehled.VProdleni.OrderByDescending(p => p.PenalizovaneDny).ThenBy(p => p.TicketId))
        {
            var duvody = p.Duvody.Select(PenaltyReasonFormatter.Text).ToList();
            var radek = list.Radek(
                p.TicketId.ToString(CultureInfo.InvariantCulture),
                p.TypZaznamu.Trim(),
                p.Dulezitost?.Trim() ?? string.Empty,
                p.Strucne ?? string.Empty,
                (p.Dodavatel?.Trim() ?? string.Empty) + (p.ViceDodavatelu ? " *" : string.Empty),
                Cas(p.UDodavateleOd),
                Cas(p.Termin),
                Cas(p.ProdlouzenoDo),
                p.PenalizovaneDny.ToString(CultureInfo.InvariantCulture),
                p.PracovniDny ? "pracovní" : "kalendářní",
                string.Join(" | ", duvody),
                ServiceDeskUrlBuilder.ForTicket(p.TicketId));
            list.Cislo(radek, SloupecDny);

            if (p.PenalizovaneDnyDatumy.Count > 0)
            {
                list.Komentar(radek, SloupecDny,
                    "Penalizované dny:\n" + string.Join("\n", p.PenalizovaneDnyDatumy
                        .OrderBy(d => d)
                        .Select(d => d.ToString("dd.MM.yyyy", Cs))));
            }
            if (duvody.Count > 0)
            {
                list.Komentar(radek, SloupecOduvodneni, string.Join("\n", duvody));
            }
        }
    }

    private static void NaplnNelze(ListBuilder list, ProjectDashboardNesPanelViewModel model)
    {
        list.Radek("Tickety u dodavatele, u kterých nejde penále spočítat (chyba dat ve ServiceDesku).");
        list.PrazdnyRadek();
        list.Radek(HlavickaNelze);
        foreach (var n in model.Prehled.NelzeVyhodnotit.OrderBy(n => n.TicketId))
        {
            list.Radek(
                n.TicketId.ToString(CultureInfo.InvariantCulture),
                n.TypZaznamu.Trim(),
                n.Strucne ?? string.Empty,
                n.Dulezitost?.Trim() ?? string.Empty,
                PenaltyChybaText.Popis(n.Chyba, n.Dulezitost));
        }
    }

    private static string Cas(DateTime? d) => d?.ToString("dd.MM.yyyy HH:mm", Cs) ?? string.Empty;

    private static void PridejList(WorkbookPart workbookPart, Sheets sheets, uint id, string nazev, Action<ListBuilder> napln)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = id, Name = nazev });

        var builder = new ListBuilder(sheetData);
        napln(builder);
        builder.ZapisKomentare(worksheetPart, id);
    }

    /// <summary>Řádky s explicitními odkazy buněk (komentáře se na ně váží) + komentáře listu.</summary>
    private sealed class ListBuilder(SheetData sheetData)
    {
        private readonly List<(uint Radek, int Sloupec, string Text)> _komentare = [];
        private uint _radek;

        public Row Radek(params string[] hodnoty)
        {
            _radek++;
            var row = new Row { RowIndex = _radek };
            for (var i = 0; i < hodnoty.Length; i++)
            {
                row.Append(new Cell
                {
                    CellReference = Odkaz(_radek, i),
                    DataType = CellValues.String,
                    CellValue = new CellValue(hodnoty[i] ?? string.Empty)
                });
            }
            sheetData.Append(row);
            return row;
        }

        public void PrazdnyRadek()
        {
            _radek++;
            sheetData.Append(new Row { RowIndex = _radek });
        }

        /// <summary>Buňku přepne na číslo, aby šel sloupec v Excelu sečíst.</summary>
        public void Cislo(Row row, int sloupec)
            => row.Elements<Cell>().ElementAt(sloupec).DataType = CellValues.Number;

        public void Komentar(Row row, int sloupec, string text) => _komentare.Add((row.RowIndex!.Value, sloupec, text));

        public void ZapisKomentare(WorksheetPart worksheetPart, uint listId)
        {
            if (_komentare.Count == 0)
            {
                return;
            }

            // comments.xml řadí komentáře podle buňky (řádek, sloupec).
            var serazene = _komentare.OrderBy(k => k.Radek).ThenBy(k => k.Sloupec).ToList();

            var commentsPart = worksheetPart.AddNewPart<WorksheetCommentsPart>();
            var commentList = new CommentList();
            foreach (var (radek, sloupec, text) in serazene)
            {
                commentList.Append(new Comment(new CommentText(new Text(text) { Space = SpaceProcessingModeValues.Preserve }))
                {
                    Reference = Odkaz(radek, sloupec),
                    AuthorId = 0U
                });
            }
            commentsPart.Comments = new Comments(new Authors(new Author("PM Tracker")), commentList);

            // Excel ukáže komentář jen s VML tvarem; bez něj sešit při otevření „opravuje".
            var vmlPart = worksheetPart.AddNewPart<VmlDrawingPart>();
            using (var writer = new StreamWriter(vmlPart.GetStream(FileMode.Create), new UTF8Encoding(false)))
            {
                writer.Write(Vml(serazene, listId));
            }
            worksheetPart.Worksheet.Append(new LegacyDrawing { Id = worksheetPart.GetIdOfPart(vmlPart) });
        }

        private static string Vml(IReadOnlyList<(uint Radek, int Sloupec, string Text)> komentare, uint listId)
        {
            var sb = new StringBuilder();
            sb.Append("<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");
            sb.Append(CultureInfo.InvariantCulture, $"<o:shapelayout v:ext=\"edit\"><o:idmap v:ext=\"edit\" data=\"{listId}\"/></o:shapelayout>");
            sb.Append("<v:shapetype id=\"_x0000_t202\" coordsize=\"21600,21600\" o:spt=\"202\" path=\"m,l,21600r21600,l21600,xe\">");
            sb.Append("<v:stroke joinstyle=\"miter\"/><v:path gradientshapeok=\"t\" o:connecttype=\"rect\"/></v:shapetype>");

            for (var i = 0; i < komentare.Count; i++)
            {
                var (radek, sloupec, text) = komentare[i];
                var radek0 = radek - 1;
                // Výška podle počtu řádků textu, aby se dlouhé odůvodnění vešlo bez ručního roztahování.
                var radku = Math.Clamp(text.Split('\n').Length + text.Length / 60, 3, 30);
                var shapeId = listId * 1024 + (uint)i + 1;
                sb.Append(CultureInfo.InvariantCulture,
                    $"<v:shape id=\"_x0000_s{shapeId}\" type=\"#_x0000_t202\" style=\"position:absolute;margin-left:60pt;margin-top:2pt;width:320pt;height:{radku * 13}pt;z-index:{i + 1};visibility:hidden\" fillcolor=\"#ffffe1\" o:insetmode=\"auto\">");
                sb.Append("<v:fill color2=\"#ffffe1\"/><v:shadow on=\"t\" color=\"black\" obscured=\"t\"/><v:path o:connecttype=\"none\"/>");
                sb.Append("<v:textbox style=\"mso-direction-alt:auto\"><div style=\"text-align:left\"></div></v:textbox>");
                sb.Append("<x:ClientData ObjectType=\"Note\"><x:MoveWithCells/><x:SizeWithCells/>");
                sb.Append(CultureInfo.InvariantCulture,
                    $"<x:Anchor>{sloupec + 1}, 15, {radek0}, 2, {sloupec + 6}, 15, {radek0 + radku}, 4</x:Anchor>");
                sb.Append("<x:AutoFill>False</x:AutoFill>");
                sb.Append(CultureInfo.InvariantCulture, $"<x:Row>{radek0}</x:Row><x:Column>{sloupec}</x:Column>");
                sb.Append("</x:ClientData></v:shape>");
            }

            sb.Append("</xml>");
            return sb.ToString();
        }

        private static string Odkaz(uint radek, int sloupec)
        {
            // Sloupců je nejvýš 12 → jedno písmeno stačí; obecný převod pro jistotu.
            var pismena = string.Empty;
            for (var n = sloupec; n >= 0; n = n / 26 - 1)
            {
                pismena = (char)('A' + n % 26) + pismena;
            }
            return pismena + radek.ToString(CultureInfo.InvariantCulture);
        }
    }
}
