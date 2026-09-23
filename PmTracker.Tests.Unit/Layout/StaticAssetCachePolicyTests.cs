using FluentAssertions;
using PmTracker.Web.Extensions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class StaticAssetCachePolicyTests
{
    [Theory]
    [InlineData("/assets/gov/components/core.esm.js", false)]
    [InlineData("/assets/gov/components/core.esm.js", true)]
    [InlineData("/assets/gov/fonts/roboto-regular.woff2", false)]
    [InlineData("/assets/gov/styles/templates.css", true)]
    public void GovAssety_SeCachujiNatrvalo(string path, bool isDevelopment)
    {
        StaticAssetCachePolicy.Resolve(path, isDevelopment).Should().Be(StaticAssetCachePolicy.Immutable);
    }

    [Theory]
    [InlineData("/js/site.js")]
    [InlineData("/css/site.css")]
    public void AplikacniJsACss_SeVeVyvojiNecachuji(string path)
    {
        // ESM sub-importy asp-append-version nevidí → bez no-cache by změny JS nedorazily.
        StaticAssetCachePolicy.Resolve(path, isDevelopment: true).Should().Be(StaticAssetCachePolicy.NoCache);
    }

    [Theory]
    [InlineData("/js/site.js", false)]
    [InlineData("/css/site.css", false)]
    [InlineData("/assets/icons/components/x.svg", false)]
    [InlineData("/assets/icons/components/x.svg", true)]
    [InlineData("/images/zapiska-logo.svg", false)]
    public void OstatniSoubory_BezZmenyHlavicky(string path, bool isDevelopment)
    {
        // Ikony nemají hash v názvu (gov je verzuje jen ?v=4.7.0) → nesmí být immutable.
        StaticAssetCachePolicy.Resolve(path, isDevelopment).Should().BeNull();
    }
}
