using System.Globalization;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public sealed class OpenXmlWordRecordDeadlinesCellWriter : IWordExportRecordDeadlinesCellWriter
{
    public void Append(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Založeno: ", Bold: true),
                new OpenXmlWordElements.TextSegment(record.DatumZalozeni.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture))
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 40,
            after: 20));

        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Aktuální termín: ", Bold: true),
                new OpenXmlWordElements.TextSegment(record.Termin.HasValue ? record.Termin.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : "-")
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 0,
            after: 20));

        if (record.HistorieTerminu.Count == 0)
        {
            return;
        }

        cell.Append(OpenXmlWordElements.CreateParagraph("Historie termínů:", bold: true, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 20, after: 20));
        foreach (var date in record.HistorieTerminu.OrderByDescending(x => x))
        {
            cell.Append(OpenXmlWordElements.CreateParagraph(date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), strike: true, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 0, after: 20));
        }
    }
}
