using Microsoft.AspNetCore.Routing;

namespace PmTracker.Web.Services.Records;

public sealed class RecordUiFlowResolver : IRecordUiFlowResolver
{
    private const string UiContextProject = "project";
    private const string UiContextMeeting = "meeting";
    private const string RecordsTab = "zaznamy";

    public RecordCommentAjaxFlow ResolveCommentAjaxFlow(string? uiContext, int projektId, int zaznamId, int? meetingId, int? kontextJednaniId = null)
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
            RefreshScope = "record-comments",
            ControllerName = "Zaznamy",
            ActionName = "RecordCommentsPartial",
            // kontextJednaniId: panel vyjádření v detailu jednání po obnovení dál předvybírá to jednání.
            RouteValues = new RouteValueDictionary(new { projektId, zaznamId, kontextJednaniId }),
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
