using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSwitchTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormSwitch()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "notifikace",
            Label = "Zasílat notifikace"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-switch", html);
        Assert.Contains("name=\"notifikace\"", html);
        Assert.Contains("Zasílat notifikace", html);
    }

    [Fact]
    public void Checked_AddsChecked()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "n",
            Label = "N",
            Checked = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("checked", html);
    }

    [Fact]
    public void Disabled_AddsDisabled()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "n",
            Label = "N",
            Disabled = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("disabled", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmSwitchTagHelper
        {
            Name = "n",
            Label = "<iframe>x</iframe>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<iframe>x", html);
        Assert.Contains("&lt;iframe&gt;", html);
    }
}
