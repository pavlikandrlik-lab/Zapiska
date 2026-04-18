using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmRadioTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormRadio()
    {
        var tagHelper = new PmRadioTagHelper
        {
            Name = "priorita",
            Value = "vysoka",
            Label = "Vysoká"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-radio", html);
        Assert.Contains("name=\"priorita\"", html);
        Assert.Contains("value=\"vysoka\"", html);
        Assert.Contains("Vysoká", html);
    }

    [Fact]
    public void Checked_AddsChecked()
    {
        var tagHelper = new PmRadioTagHelper
        {
            Name = "p",
            Value = "a",
            Label = "A",
            Checked = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("checked", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmRadioTagHelper
        {
            Name = "p",
            Value = "v",
            Label = "<script>y</script>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<script>y", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
