namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportMeetingProjectIdQueryHandler(IExportDataStore dataStore) : IExportMeetingProjectIdQueryHandler
{
    public int Handle(int jednaniId)
        => dataStore.BuildJednaniDetail(jednaniId).ProjektId;
}
