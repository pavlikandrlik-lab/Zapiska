using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmDialogTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovDialogWithTitleSlot()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "confirm-delete",
            Title = "Potvrdit smazání"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "<p>Opravdu smazat?</p>");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-dialog", output.TagName);
        Assert.Equal("confirm-delete", output.Attributes["id"]?.Value?.ToString());
        var html = output.Content.GetContent();
        Assert.Contains("<h3 slot=\"title\">Potvrdit smazání</h3>", html);
        Assert.Contains("<p>Opravdu smazat?</p>", html);
    }

    [Fact]
    public async Task Open_SetsOpenAttribute()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "d",
            Title = "T",
            Open = true
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("true", output.Attributes["open"]?.Value?.ToString());
    }

    [Fact]
    public async Task XssInTitle_IsEscaped()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "d",
            Title = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task TitleWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmDialogTagHelper
        {
            Id = "d",
            Title = "Potvrdit změnu"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-dialog", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = output.Content.GetContent();
        Assert.Contains("Potvrdit změnu", html);
        Assert.DoesNotContain("&#", html);
    }
}
