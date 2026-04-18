using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmCheckboxTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormCheckboxWithLabel()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "souhlas",
            Label = "Souhlasím s podmínkami",
            Value = "1"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-checkbox", html);
        Assert.Contains("name=\"souhlas\"", html);
        Assert.Contains("value=\"1\"", html);
        Assert.Contains("Souhlasím s podmínkami", html);
    }

    [Fact]
    public void Checked_AddsCheckedAttribute()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "aktivni",
            Label = "Aktivní",
            Checked = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("checked", html);
    }

    [Fact]
    public void Disabled_AddsDisabledAttribute()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "a",
            Label = "A",
            Disabled = true
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("disabled", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmCheckboxTagHelper
        {
            Name = "x",
            Label = "<script>x</script>"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<script>x", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
