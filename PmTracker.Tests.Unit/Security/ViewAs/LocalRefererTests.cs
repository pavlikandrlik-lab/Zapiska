using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class LocalRefererTests
{
    private static HttpRequest Request(string? referer)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("pmtracker.local");
        if (referer is not null)
        {
            context.Request.Headers.Referer = referer;
        }
        return context.Request;
    }

    [Fact]
    public void Resolve_SameHost_ReturnsPathAndQuery()
        => LocalReferer.Resolve(Request("https://pmtracker.local/Projekty/Detail/5?tab=tym&asUser=3"))
            .Should().Be("/Projekty/Detail/5?tab=tym&asUser=3");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://evil.example/Projekty")]
    [InlineData("/Projekty")]
    public void Resolve_ForeignOrInvalid_ReturnsNull(string? referer)
        => LocalReferer.Resolve(Request(referer)).Should().BeNull();
}
