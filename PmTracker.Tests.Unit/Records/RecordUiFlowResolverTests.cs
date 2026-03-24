using FluentAssertions;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordUiFlowResolverTests
{
    private readonly RecordUiFlowResolver _sut = new();

    [Fact]
    public void ResolveCommentAjaxFlow_ShouldReturnMeetingRefresh_WhenMeetingContextProvided()
    {
        var result = _sut.ResolveCommentAjaxFlow("meeting", 17, 31, 44);

        result.RefreshScope.Should().Be("meeting-task-item");
        result.ControllerName.Should().Be("Jednani");
        result.ActionName.Should().Be("TaskItemPartial");
        result.RouteValues["jednaniId"].Should().Be(44);
        result.RouteValues["zaznamId"].Should().Be(31);
        result.UiContext.Should().Be("meeting");
        result.MeetingId.Should().Be(44);
        result.Tab.Should().BeNull();
    }

    [Fact]
    public void ResolveCommentAjaxFlow_ShouldReturnProjectRefresh_WhenMeetingContextMissingMeetingId()
    {
        var result = _sut.ResolveCommentAjaxFlow("meeting", 17, 31, null);

        result.RefreshScope.Should().Be("record-card");
        result.ControllerName.Should().Be("Zaznamy");
        result.ActionName.Should().Be("RecordCardPartial");
        result.RouteValues["projektId"].Should().Be(17);
        result.RouteValues["zaznamId"].Should().Be(31);
        result.UiContext.Should().Be("project");
        result.MeetingId.Should().BeNull();
        result.Tab.Should().Be("zaznamy");
    }

    [Fact]
    public void ResolveCommentRedirectFlow_ShouldReturnMeetingDetail_WhenMeetingContextProvided()
    {
        var result = _sut.ResolveCommentRedirectFlow("meeting", 17, 55, "/ignored");

        result.IsLocalRedirect.Should().BeFalse();
        result.ControllerName.Should().Be("Jednani");
        result.ActionName.Should().Be("Detail");
        result.RouteValues["id"].Should().Be(55);
    }

    [Fact]
    public void ResolveCommentRedirectFlow_ShouldPreferProjectRoute_WhenProjectContextAndLocalUrlProvided()
    {
        var result = _sut.ResolveCommentRedirectFlow("project", 17, null, "/lokalni");

        result.IsLocalRedirect.Should().BeFalse();
        result.ControllerName.Should().Be("Projekty");
        result.ActionName.Should().Be("Detail");
        result.RouteValues["id"].Should().Be(17);
        result.RouteValues["tab"].Should().Be("zaznamy");
    }

    [Fact]
    public void ResolveCommentRedirectFlow_ShouldUseLocalUrl_WhenContextUnknownAndLocalUrlProvided()
    {
        var result = _sut.ResolveCommentRedirectFlow("other", 17, null, "/lokalni");

        result.IsLocalRedirect.Should().BeTrue();
        result.LocalUrl.Should().Be("/lokalni");
        result.ControllerName.Should().Be("Projekty");
        result.ActionName.Should().Be("Detail");
    }

    [Fact]
    public void ResolveCommentRedirectFlow_ShouldReturnProjectRoute_WhenContextUnknownAndNoLocalUrl()
    {
        var result = _sut.ResolveCommentRedirectFlow("other", 17, null, null);

        result.IsLocalRedirect.Should().BeFalse();
        result.ControllerName.Should().Be("Projekty");
        result.ActionName.Should().Be("Detail");
        result.RouteValues["id"].Should().Be(17);
        result.RouteValues["tab"].Should().Be("zaznamy");
    }
}
