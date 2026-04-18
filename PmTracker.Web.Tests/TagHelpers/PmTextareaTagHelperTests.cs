using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmTextareaTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormControlWithTextarea()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "poznamka",
            Label = "Poznámka",
            Rows = 4
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-control", html);
        Assert.Contains("<gov-form-input", html);
        Assert.Contains("<textarea", html);
        Assert.Contains("name=\"poznamka\"", html);
        Assert.Contains("rows=\"4\"", html);
        Assert.Contains(">Poznámka<", html);
    }

    [Fact]
    public void HelpText_RendersFormMessage()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "komentar",
            Label = "Komentář",
            Help = "Max. 500 znaků"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-message slot=\"bottom\">Max. 500 znaků</gov-form-message>", html);
    }

    [Fact]
    public void Error_RendersErrorVariantAndInvalid()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "p",
            Label = "P",
            Error = "Je potřeba něco napsat"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("invalid=\"invalid\"", html);
        Assert.Contains("variant=\"error\"", html);
        Assert.Contains("Je potřeba něco napsat", html);
    }

    [Fact]
    public void Disabled_AddsDisabledAttribute()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "p",
            Label = "P",
            Disabled = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("disabled", html);
    }

    [Fact]
    public void XssInHelp_IsEscaped()
    {
        var tagHelper = new PmTextareaTagHelper
        {
            Name = "p",
            Label = "P",
            Help = "<img src=x onerror=alert(1)>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;img src=x", html);
    }
}
