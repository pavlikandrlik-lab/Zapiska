using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmSkeletonTagHelperTests
{
    [Fact]
    public void Default_RendersGovSkeletonMediumWithoutShapeAttr()
    {
        var tagHelper = new PmSkeletonTagHelper();

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-skeleton", html);
        Assert.Contains("size=\"m\"", html);
        // Default shape nemá emitovat shape atribut (gov-skeleton bere default tvar interně)
        Assert.DoesNotContain("shape=", html);
    }

    [Fact]
    public void CircleShape_SetsShapeAttribute()
    {
        var tagHelper = new PmSkeletonTagHelper
        {
            Shape = PmSkeletonShape.Circle
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("shape=\"circle\"", html);
    }

    [Fact]
    public void LargeSize_SetsSizeAttribute()
    {
        var tagHelper = new PmSkeletonTagHelper
        {
            Size = PmComponentSize.Large
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("size=\"l\"", html);
    }
}
