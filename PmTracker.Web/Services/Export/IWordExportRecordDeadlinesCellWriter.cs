using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

public interface IWordExportRecordDeadlinesCellWriter
{
    void Append(TableCell cell, PdfExportRecordViewModel record);
}
