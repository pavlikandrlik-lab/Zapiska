using System.Threading.Tasks;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Web.Tests.TagHelpers;

public sealed class PmFieldTagHelperTests
{
    [Fact]
    public async Task BasicField_ContainsIdentifierAttribute()
    {
        var helper = new PmFieldTagHelper { Name = "email", Label = "E-mail" };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("identifier=\"pm-field-email\"", html);
    }

    [Fact]
    public async Task BasicField_DoesNotContainDuplicateInput()
    {
        var helper = new PmFieldTagHelper { Name = "jmeno", Label = "Jméno" };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        // Nesmí obsahovat žádný light-DOM <input> ani <span class="element">
        Assert.DoesNotContain("<input ", html);
        Assert.DoesNotContain("<span class=\"element\">", html);
    }

    [Fact]
    public async Task BasicField_RendersGovFormInput()
    {
        var helper = new PmFieldTagHelper { Name = "test", Label = "Test" };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("<gov-form-input", html);
        Assert.Contains("</gov-form-input>", html);
    }

    [Fact]
    public async Task BasicField_LabelHasCorrectFor()
    {
        var helper = new PmFieldTagHelper { Name = "email", Label = "E-mail" };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("for=\"pm-field-email\"", html);
    }

    [Fact]
    public async Task FieldWithError_ContainsGovFormMessage()
    {
        var helper = new PmFieldTagHelper { Name = "chyba", Label = "S chybou", Error = "Pole je povinné" };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("variant=\"error\"", html);
        Assert.Contains("Pole je povinné", html);
    }

    [Fact]
    public async Task FieldWithHelp_ContainsHelpMessage()
    {
        var helper = new PmFieldTagHelper { Name = "with-help", Label = "Help Field", Help = "Nápověda" };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("gov-form-message", html);
        Assert.Contains("Nápověda", html);
    }

    [Fact]
    public async Task RequiredField_ContainsRequiredAttribute()
    {
        var helper = new PmFieldTagHelper { Name = "req", Label = "Required", Required = true };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        // required atribut musí být v gov-form-input
        Assert.Contains("gov-form-input", html);
        Assert.Contains("required", html);
    }

    [Fact]
    public async Task XssInLabel_IsEscaped()
    {
        var helper = new PmFieldTagHelper
        {
            Name = "pole",
            Label = "<script>alert(1)</script>"
        };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task XssInError_IsEscaped()
    {
        var helper = new PmFieldTagHelper
        {
            Name = "pole",
            Label = "Pole",
            Error = "<img src=x onerror=alert(1)>"
        };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.DoesNotContain("<img src=x onerror=alert", html);
        Assert.Contains("&lt;img src=x", html);
    }

    [Fact]
    public async Task LabelWithDiacritics_PreservesUnicode()
    {
        var helper = new PmFieldTagHelper
        {
            Name = "pole",
            Label = "E-mail uživatele"
        };
        var ctx = TagHelperTestHelpers.MakeContext("pm-field");
        var output = TagHelperTestHelpers.MakeOutput("pm-field");

        await helper.ProcessAsync(ctx, output);

        var html = TagHelperTestHelpers.Render(output);
        Assert.Contains("E-mail uživatele", html);
        Assert.DoesNotContain("&#", html);
    }
}
