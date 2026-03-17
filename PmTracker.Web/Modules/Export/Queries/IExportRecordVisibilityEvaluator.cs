using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Modules.Export.Queries;

public interface IExportRecordVisibilityEvaluator
{
    bool IsVisibleForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate,
        DateTime? previousMeetingDate);
}
