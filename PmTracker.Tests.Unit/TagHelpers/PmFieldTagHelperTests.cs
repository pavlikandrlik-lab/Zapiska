using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.TagHelpers;
using PmTracker.Web.TagHelpers;

namespace PmTracker.Tests.Unit.TagHelpers;

public sealed class PmFieldTagHelperTests
{
    private static async Task<TagHelperOutput> RenderAsync(PmFieldTagHelper helper)
    {
        var ctx = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput(
            "pm-field",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(ctx, output);
        return output;
    }

    private static string GetAllHtml(TagHelperOutput output) =>
        output.PreContent.GetContent() + output.Content.GetContent() + output.PostContent.GetContent();

    [Fact]
    public async Task Rendruje_GovFormControl_SLabelAInput()
    {
        var helper = new PmFieldTagHelper
        {
            Name = "email",
            Label = "E-mail",
            InputType = "email"
        };
        var output = await RenderAsync(helper);
        output.TagName.Should().Be("gov-form-control");

        var html = GetAllHtml(output);
        html.Should().Contain("<gov-form-label");
        html.Should().Contain("E-mail");
        html.Should().Contain("<gov-form-input");
        html.Should().Contain("name=\"email\"");
    }

    [Fact]
    public async Task Required_NastaviPovinnyAtribut_VInputu()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X", Required = true };
        var output = await RenderAsync(helper);
        var html = GetAllHtml(output);
        html.Should().Contain("required");
    }

    [Fact]
    public async Task Error_VlozGovFormMessageError()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X", Error = "Povinné pole" };
        var output = await RenderAsync(helper);
        var html = GetAllHtml(output);
        html.Should().Contain("<gov-form-message");
        html.Should().Contain("variant=\"error\"");
        html.Should().Contain("Povinné pole");
    }

    [Fact]
    public async Task Help_VlozGovFormMessageDefault()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X", Help = "Zadejte e-mail" };
        var output = await RenderAsync(helper);
        var html = GetAllHtml(output);
        html.Should().Contain("<gov-form-message");
        html.Should().Contain("Zadejte e-mail");
    }

    [Fact]
    public async Task InputType_Default_JeText()
    {
        var helper = new PmFieldTagHelper { Name = "x", Label = "X" };
        var output = await RenderAsync(helper);
        var html = GetAllHtml(output);
        html.Should().Contain("type=\"text\"");
    }

    [Fact]
    public async Task Value_Propage_DoInputu()
    {
        var helper = new PmFieldTagHelper { Name = "email", Label = "E-mail", Value = "a@b.cz" };
        var output = await RenderAsync(helper);
        var html = GetAllHtml(output);
        html.Should().Contain("value=\"a@b.cz\"");
    }
}
