using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportRecordVisibilityEvaluator : IExportRecordVisibilityEvaluator
{
    public bool IsVisibleForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate,
        DateTime? previousMeetingDate)
    {
        if (record.DatumZalozeni.Date > anchorMeetingDate.Date)
        {
            return false;
        }

        var statusAtAnchorMeeting = ResolveTaskStatusAtDate(record.StavUkoluId, statusHistory, anchorMeetingDate);
        if (!IsFinalTaskStatus(statusAtAnchorMeeting, taskStates))
        {
            return true;
        }

        if (!previousMeetingDate.HasValue)
        {
            return false;
        }

        var statusAtPreviousMeeting = ResolveTaskStatusAtDate(record.StavUkoluId, statusHistory, previousMeetingDate.Value);
        return !IsFinalTaskStatus(statusAtPreviousMeeting, taskStates);
    }

    private static int? ResolveTaskStatusAtDate(
        int? currentStatusId,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime targetDate)
    {
        var resolvedStatusId = currentStatusId;
        foreach (var change in statusHistory)
        {
            if (change.DatumZmeny.Date <= targetDate.Date)
            {
                continue;
            }

            if (resolvedStatusId.HasValue && resolvedStatusId.Value == change.NovyStav)
            {
                resolvedStatusId = change.PuvodniStav;
            }
        }

        return resolvedStatusId;
    }

    private static bool IsFinalTaskStatus(
        int? statusId,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates)
    {
        return statusId.HasValue
            && taskStates.GetValueOrDefault(statusId.Value)?.IsFinal == true;
    }
}
