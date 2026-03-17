using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public sealed class OpenXmlWordRecordPeopleCellWriter : IWordExportRecordPeopleCellWriter
{
    public void Append(TableCell cell, PdfExportRecordViewModel record)
    {
        cell.Append(OpenXmlWordElements.CreateRichParagraph(
            new[]
            {
                new OpenXmlWordElements.TextSegment("Vlastník: ", Bold: true),
                new OpenXmlWordElements.TextSegment(record.Vlastnik)
            },
            sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints,
            before: 40,
            after: 30));

        if (record.Spoluprace.Count == 0)
        {
            return;
        }

        cell.Append(OpenXmlWordElements.CreateParagraph("Spolupráce:", bold: true, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 20, after: 20));
        foreach (var person in record.Spoluprace)
        {
            cell.Append(OpenXmlWordElements.CreateParagraph(person, sizeHalfPoints: OpenXmlWordElements.BaseFontHalfPoints, before: 0, after: 20));
        }
    }
}
