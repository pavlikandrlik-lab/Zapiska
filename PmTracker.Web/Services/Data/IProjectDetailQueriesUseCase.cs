using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProjectDetailQueriesUseCase
{
    ProjektDetailViewModel BuildProjektDetail(int id, IProjectDetailComposition composition);
}
