using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Projects.Queries;

public interface IBuildProjektDetailQueryHandler
{
    ProjektDetailViewModel Handle(int id);
}
