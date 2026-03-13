namespace PmTracker.Web.Modules.Export;

public interface IExportTemplateQueries
{
    ExportTemplateQueryResult GetProjectTemplate(int projektId);
    ExportTemplateQueryResult GetMeetingTemplate(int jednaniId);
    ExportTemplateQueryResult GetTaskTemplate(int projektId, int zaznamId);
}
