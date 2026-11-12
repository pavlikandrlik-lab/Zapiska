using FluentAssertions;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordUiFlowResolverTests
{
    private readonly RecordUiFlowResolver _sut = new();

    [Fact]
    public void ResolveCommentAjaxFlow_MeetingContext_ObnoviPanelVyjadreni()
    {
        var result = _sut.ResolveCommentAjaxFlow("meeting", 17, 31, 44, kontextJednaniId: 44);

        result.RefreshScope.Should().Be("record-comments", "obnovení bloku úkolu jednání (_TaskItemPartial) zaniklo (spec 2026-11-10 R8)");
        result.ActionName.Should().Be("RecordCommentsPartial");
        result.RouteValues["kontextJednaniId"].Should().Be(44);
        result.MeetingId.Should().BeNull();
    }

    [Fact]
    public void ResolveCommentAjaxFlow_ProjectRefresh_KeepsMeetingContext()
    {
        var result = _sut.ResolveCommentAjaxFlow("project", 17, 31, null, kontextJednaniId: 55);

        result.RefreshScope.Should().Be("record-comments");
        result.RouteValues["kontextJednaniId"].Should().Be(55, "obnovený panel vyjádření musí dál předvybírat jednání");
    }

    [Fact]
    public void ResolveCommentAjaxFlow_ShouldReturnProjectRefresh_WhenMeetingContextMissingMeetingId()
    {
        var result = _sut.ResolveCommentAjaxFlow("meeting", 17, 31, null);

        result.RefreshScope.Should().Be("record-comments");
        result.ControllerName.Should().Be("Zaznamy");
        result.ActionName.Should().Be("RecordCommentsPartial");
        result.RouteValues["projektId"].Should().Be(17);
        result.RouteValues["zaznamId"].Should().Be(31);
        result.UiContext.Should().Be("project");
        result.MeetingId.Should().BeNull();
        result.Tab.Should().Be("zaznamy");
    }

    [Fact]
    public void ResolveCommentRedirectFlow_MeetingContext_VratiNaZaznamyProjektu()
    {
        // „meeting“ posílaly jen formuláře zrušeného _TaskItemPartial (spec 2026-11-10 R8).
        var result = _sut.ResolveCommentRedirectFlow("meeting", 17, 55, null);

        result.IsLocalRedirect.Should().BeFalse();
        result.ControllerName.Should().Be("Projekty");
        result.ActionName.Should().Be("Detail");
        result.RouteValues["id"].Should().Be(17);
        result.RouteValues["tab"].Should().Be("zaznamy");
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
