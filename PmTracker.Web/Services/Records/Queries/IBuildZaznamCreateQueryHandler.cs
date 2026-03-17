using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public interface IBuildZaznamCreateQueryHandler
{
    ZaznamEditViewModel Handle(int projektId, int? jednaniId = null);
}
