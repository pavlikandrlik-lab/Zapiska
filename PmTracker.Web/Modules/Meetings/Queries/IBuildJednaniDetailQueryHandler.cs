using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public interface IBuildJednaniDetailQueryHandler
{
    JednaniDetailViewModel Handle(int id);
}
