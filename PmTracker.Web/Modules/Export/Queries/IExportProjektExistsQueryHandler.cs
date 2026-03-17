namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportProjektExistsQueryHandler
{
    bool Handle(int projektId);
}
