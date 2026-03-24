using Microsoft.AspNetCore.Routing;

namespace PmTracker.Web.Services.Records;

public interface IRecordUiFlowResolver
{
    RecordCommentAjaxFlow ResolveCommentAjaxFlow(string? uiContext, int projektId, int zaznamId, int? meetingId);

    RecordCommentRedirectFlow ResolveCommentRedirectFlow(string? uiContext, int projektId, int? meetingId, string? localReturnUrl);
}

public sealed record class RecordCommentAjaxFlow
{
    public required string RefreshScope { get; init; }

    public required string ControllerName { get; init; }

    public required string ActionName { get; init; }

    public required RouteValueDictionary RouteValues { get; init; }

    public required string UiContext { get; init; }

    public string? Tab { get; init; }

    public int? MeetingId { get; init; }
}

public sealed record class RecordCommentRedirectFlow
{
    public required string ControllerName { get; init; }

    public required string ActionName { get; init; }

    public required RouteValueDictionary RouteValues { get; init; }

    public string? LocalUrl { get; init; }

    public bool IsLocalRedirect => !string.IsNullOrWhiteSpace(LocalUrl);
}
