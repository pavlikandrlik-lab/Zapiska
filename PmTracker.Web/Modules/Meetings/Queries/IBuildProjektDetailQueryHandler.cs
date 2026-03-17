using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public interface IBuildProjektDetailQueryHandler
{
    ProjektDetailViewModel Handle(int id);
}
