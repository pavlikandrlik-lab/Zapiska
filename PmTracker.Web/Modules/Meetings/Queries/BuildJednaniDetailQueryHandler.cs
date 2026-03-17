using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public sealed class BuildJednaniDetailQueryHandler(IMeetingsDataStore dataStore) : IBuildJednaniDetailQueryHandler
{
    public JednaniDetailViewModel Handle(int id)
        => dataStore.BuildJednaniDetail(id);
}
