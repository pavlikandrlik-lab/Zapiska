using Microsoft.AspNetCore.Routing;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Kam po uložení vyjádření obnovit / přesměrovat. Kontext „meeting“ (blok úkolu jednání,
/// _TaskItemPartial) zanikl se spec 2026-11-10 R8 — vyjádření se v jednání obnovují panelem
/// záznamu (RecordCommentsPartial s kontextJednaniId).
/// </summary>
public sealed class RecordUiFlowResolver : IRecordUiFlowResolver
{
    private const string UiContextProject = "project";
    private const string RecordsTab = "zaznamy";

    public RecordCommentAjaxFlow ResolveCommentAjaxFlow(string? uiContext, int projektId, int zaznamId, int? meetingId, int? kontextJednaniId = null)
    {
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
