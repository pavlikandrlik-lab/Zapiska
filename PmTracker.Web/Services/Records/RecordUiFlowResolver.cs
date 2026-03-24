using Microsoft.AspNetCore.Routing;

namespace PmTracker.Web.Services.Records;

public sealed class RecordUiFlowResolver : IRecordUiFlowResolver
{
    private const string UiContextProject = "project";
    private const string UiContextMeeting = "meeting";
    private const string RecordsTab = "zaznamy";

    public RecordCommentAjaxFlow ResolveCommentAjaxFlow(string? uiContext, int projektId, int zaznamId, int? meetingId)
    {
        if (string.Equals(uiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase) && meetingId.HasValue)
        {
            return new RecordCommentAjaxFlow
            {
                RefreshScope = "meeting-task-item",
                ControllerName = "Jednani",
                ActionName = "TaskItemPartial",
                RouteValues = new RouteValueDictionary(new { jednaniId = meetingId.Value, zaznamId }),
                UiContext = UiContextMeeting,
                MeetingId = meetingId.Value
            };
        }

        return new RecordCommentAjaxFlow
        {
            RefreshScope = "record-card",
            ControllerName = "Zaznamy",
            ActionName = "RecordCardPartial",
            RouteValues = new RouteValueDictionary(new { projektId, zaznamId }),
            UiContext = UiContextProject,
            Tab = RecordsTab
        };
    }

    public RecordCommentRedirectFlow ResolveCommentRedirectFlow(string? uiContext, int projektId, int? meetingId, string? localReturnUrl)
    {
        if (string.Equals(uiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase) && meetingId.HasValue)
        {
            return new RecordCommentRedirectFlow
            {
                ControllerName = "Jednani",
                ActionName = "Detail",
                RouteValues = new RouteValueDictionary(new { id = meetingId.Value })
            };
        }

        if (string.Equals(uiContext, UiContextProject, StringComparison.OrdinalIgnoreCase))
        {
            return new RecordCommentRedirectFlow
            {
                ControllerName = "Projekty",
                ActionName = "Detail",
                RouteValues = new RouteValueDictionary(new { id = projektId, tab = RecordsTab })
            };
        }

        if (!string.IsNullOrWhiteSpace(localReturnUrl))
        {
            return new RecordCommentRedirectFlow
            {
                ControllerName = "Projekty",
                ActionName = "Detail",
                RouteValues = new RouteValueDictionary(new { id = projektId, tab = RecordsTab }),
                LocalUrl = localReturnUrl
            };
        }

        return new RecordCommentRedirectFlow
        {
            ControllerName = "Projekty",
            ActionName = "Detail",
            RouteValues = new RouteValueDictionary(new { id = projektId, tab = RecordsTab })
        };
    }
}
