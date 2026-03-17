using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Queries;

public interface IBuildDeleteRecordModalQueryHandler
{
    DeleteRecordModalViewModel Handle(int projektId, int zaznamId);
}
