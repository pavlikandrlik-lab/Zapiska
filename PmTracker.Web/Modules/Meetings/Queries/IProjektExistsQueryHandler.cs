using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Meetings.Queries;

public interface IProjektExistsQueryHandler
{
    bool Handle(int id);
}
