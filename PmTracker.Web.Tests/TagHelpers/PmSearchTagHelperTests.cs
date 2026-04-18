using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSearchTagHelperTests
{
    [Fact]
    public async Task Default_RendersGovFormSearchWithInput()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Placeholder = "Hledat…" };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("gov-form-search", output.TagName);
        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("<gov-form-input", html);
        Assert.Contains("slot=\"input\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.Contains("placeholder=\"Hledat…\"", html);
        Assert.Contains("type=\"search\"", html);
    }

    [Fact]
    public async Task Default_DoesNotEmitSubmitButton()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q" };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("<gov-button", html);
    }

    [Fact]
    public async Task SubmitTrue_EmitsGovButtonWithSlotButton()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Submit = true };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("<gov-button", html);
        Assert.Contains("slot=\"button\"", html);
        Assert.Contains("native-type=\"submit\"", html);
        Assert.Contains(">Hledat</gov-button>", html);
    }

    [Fact]
    public async Task AutofocusTrue_EmitsAutofocusAttribute()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Autofocus = true };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("autofocus", html);
    }

    [Fact]
    public async Task LargeSize_SetsSizeL()
    {
        var tagHelper = new PmSearchTagHelper { Name = "q", Size = PmComponentSize.Large };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        Assert.Equal("l", output.Attributes["size"]?.Value?.ToString());
        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("size=\"l\"", html);
    }

    [Fact]
    public async Task XssInPlaceholder_IsEscaped()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Placeholder = "\" onmouseover=\"alert(1)"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("onmouseover=\"alert", html);
        Assert.Contains("&quot;", html);
    }

    [Fact]
    public async Task XssInValue_IsEscaped()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Value = "<script>alert(1)</script>"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task PlaceholderWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmSearchTagHelper
        {
            Name = "q",
            Placeholder = "Jméno, příjmení…"
        };
        var context = TagHelperTestHelpers.MakeContext();
        var output = TagHelperTestHelpers.MakeOutput("pm-search", childContent: "");
        await tagHelper.ProcessAsync(context, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("Jméno, příjmení", html);
        Assert.DoesNotContain("&#", html);
    }
}
