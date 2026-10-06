using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// gov-form-input (DS gov 4.7.0) má <c>autocomplete</c> jako boolean: Stencil vyhodnotí každý
/// řetězec kromě "false" jako true a komponenta vypíše na vnitřní input autocomplete="on".
/// <c>autocomplete="off"</c> tedy našeptávač prohlížeče zapíná — u globálního hledání pak
/// historie prohlížeče překrývá náš dropdown.
/// </summary>
public sealed class GovFormInputAutocompleteTests
{
    [Fact]
    public void GovFormInput_ZapisujeAutocompleteJakoBoolean()
    {
        var views = Directory.GetFiles(ResolvePath("PmTracker.Web/Views"), "*.cshtml", SearchOption.AllDirectories);

        var offenders = views
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"<gov-form-input\b[^>]*>")
                .Select(tag => (path, tag: tag.Value)))
            .Select(x => (x.path, value: Regex.Match(x.tag, @"\sautocomplete=""([^""]*)""")))
            .Where(x => x.value.Success && x.value.Groups[1].Value is not ("true" or "false"))
            .Select(x => $"{Path.GetFileName(x.path)}: autocomplete=\"{x.value.Groups[1].Value}\"")
            .ToList();

        string.Join("; ", offenders).Should().BeEmpty("gov-form-input bere autocomplete jen jako \"true\"/\"false\"; \"off\" se vypíše jako on");
    }
}
