using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public interface IBuildZaznamEditQueryHandler
{
    ZaznamEditViewModel Handle(int id);
}
