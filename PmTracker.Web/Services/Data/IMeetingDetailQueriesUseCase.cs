using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IMeetingDetailQueriesUseCase
{
    JednaniDetailViewModel BuildJednaniDetail(int id);
}
