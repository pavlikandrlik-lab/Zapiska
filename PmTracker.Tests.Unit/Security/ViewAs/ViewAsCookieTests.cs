using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class ViewAsCookieTests
{
    [Fact]
    public void Format_ThenTryParse_RoundTrips()
    {
        var raw = ViewAsCookie.Format(12, 345);

        raw.Should().Be("12-345");
        ViewAsCookie.TryParse(raw, out var real, out var target).Should().BeTrue();
        real.Should().Be(12);
        target.Should().Be(345);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12-")]
    [InlineData("-12")]
    [InlineData("a-b")]
    [InlineData("12-34-56")]
    [InlineData("0-5")]
    [InlineData("5-0")]
    [InlineData(" 12-34")]
    [InlineData("+12-34")]
    [InlineData("12:34")]
    public void TryParse_RejectsMalformedValues(string? raw)
    {
        ViewAsCookie.TryParse(raw, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void BuildOptions_IsHttpOnlyLaxSessionCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        var options = ViewAsCookie.BuildOptions(context.Request);

        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeTrue();
        options.SameSite.Should().Be(SameSiteMode.Lax);
        options.IsEssential.Should().BeTrue();
        options.Path.Should().Be("/");
        options.Expires.Should().BeNull("pohled jako má skončit zavřením prohlížeče");
    }

    [Fact]
    public void Delete_EmitsExpiredSetCookie()
    {
        var context = new DefaultHttpContext();

        ViewAsCookie.Delete(context);

        context.Response.Headers.SetCookie.ToString()
            .Should().StartWith($"{ViewAsCookie.Name}=;")
            .And.Contain("1970");
    }
}
