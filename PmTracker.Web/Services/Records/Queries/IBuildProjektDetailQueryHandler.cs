using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public interface IBuildProjektDetailQueryHandler
{
    ProjektDetailViewModel Handle(int id);
}
