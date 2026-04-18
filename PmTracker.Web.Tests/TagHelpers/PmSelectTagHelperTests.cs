using System.Collections.Generic;
using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSelectTagHelperTests
{
    [Fact]
    public void Default_RendersGovFormControlWithSelect()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "stav",
            Label = "Stav záznamu",
            Options = new List<PmSelectOption>
            {
                new("nova", "Nová", Selected: false, Disabled: false),
                new("rozpracovana", "Rozpracovaná", Selected: true, Disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-form-control", html);
        Assert.Contains("<gov-form-select", html);
        Assert.Contains("name=\"stav\"", html);
        Assert.Contains("identifier=\"pm-select-stav\"", html);
        Assert.Contains(">Stav záznamu<", html);
        Assert.Contains("<option value=\"nova\">Nová</option>", html);
        Assert.Contains("<option value=\"rozpracovana\" selected>Rozpracovaná</option>", html);
    }

    [Fact]
    public void DisabledOption_RendersDisabledAttribute()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "role",
            Label = "Role",
            Options = new List<PmSelectOption>
            {
                new("--", "Vyberte…", Selected: true, Disabled: true),
                new("admin", "Administrátor", Selected: false, Disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<option value=\"--\" selected disabled>Vyberte…</option>", html);
    }

    [Fact]
    public void Required_AppendsRequiredMarker()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "kategorie",
            Label = "Kategorie",
            Required = true,
            Options = new List<PmSelectOption>
            {
                new("a", "A", Selected: false, Disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<span aria-hidden=\"true\">*</span>", html);
        Assert.Contains("required", html);
    }

    [Fact]
    public void LabelWithDiacritics_PreservesUnicode()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "velikost",
            Label = "Velikost měření",
            Options = new List<PmSelectOption>
            {
                new("s", "Malá", Selected: false, Disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("Velikost měření", html);
        Assert.DoesNotContain("&#", html);
    }

    [Fact]
    public void XssInLabel_IsEscaped()
    {
        var tagHelper = new PmSelectTagHelper
        {
            Name = "pole",
            Label = "<script>alert('xss')</script>",
            Options = new List<PmSelectOption>
            {
                new("a", "A", Selected: false, Disabled: false)
            }
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }
}
