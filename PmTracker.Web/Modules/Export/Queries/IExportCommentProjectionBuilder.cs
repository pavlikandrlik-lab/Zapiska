using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportCommentProjectionBuilder
{
    List<VyjadreniEntity> OrderForExport(IReadOnlyList<VyjadreniEntity> comments, IReadOnlyDictionary<int, JednaniEntity> meetingById);

    List<VyjadreniEntity> ApplyLimit(IReadOnlyList<VyjadreniEntity> comments);
}
