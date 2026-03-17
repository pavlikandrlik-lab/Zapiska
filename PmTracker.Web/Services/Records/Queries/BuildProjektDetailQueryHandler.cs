using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public sealed class BuildProjektDetailQueryHandler(IRecordsDataStore dataStore) : IBuildProjektDetailQueryHandler
{
    public ProjektDetailViewModel Handle(int id)
        => dataStore.BuildProjektDetail(id);
}
