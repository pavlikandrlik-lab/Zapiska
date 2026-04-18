using PmTracker.Web.TagHelpers;
using Xunit;

namespace PmTracker.Web.Tests.TagHelpers;

public class PmPaginationTagHelperTests
{
    [Fact]
    public void Default_RendersGovPagination()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 3,
            TotalPages = 12,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-pagination", html);
        Assert.Contains("current=\"3\"", html);
        Assert.Contains("pages=\"12\"", html);
        Assert.Contains("href-template=\"?page={0}\"", html);
    }

    [Fact]
    public void FirstPage_RendersWithCurrentOne()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 1,
            TotalPages = 5,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("current=\"1\"", html);
        Assert.Contains("pages=\"5\"", html);
    }

    [Fact]
    public void LastPage_RendersWithCurrentEqualsTotal()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 5,
            TotalPages = 5,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("current=\"5\"", html);
        Assert.Contains("pages=\"5\"", html);
    }

    [Fact]
    public void SinglePage_StillRendersGracefully()
    {
        var tagHelper = new PmPaginationTagHelper
        {
            Current = 1,
            TotalPages = 1,
            UrlTemplate = "?page={0}"
        };

        var html = TagHelperTestHelpers.Render(tagHelper);

        Assert.Contains("<gov-pagination", html);
        Assert.Contains("pages=\"1\"", html);
    }
}
