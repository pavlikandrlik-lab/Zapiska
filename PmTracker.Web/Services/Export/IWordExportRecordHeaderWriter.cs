using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public interface IWordExportRecordHeaderWriter
{
    void Append(TableCell cell, MainDocumentPart mainPart, PdfExportRecordViewModel record);
}
