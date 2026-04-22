using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using PmTracker.Web.Controllers;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Security;

/// <summary>
/// M-3: SearchController.Index a SearchController.Suggest volají drahé
/// FREETEXTTABLE queries. Bez rate limitingu může authentikovaný uživatel
/// hot-loop vytížit DB. Rate limit: 30 req / 10s per user.
/// </summary>
public sealed class SearchRateLimitingTests
{
    [Fact]
    public void Program_Should_Register_SearchRateLimiter()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Program.cs"));

        code.Should().Contain("AddRateLimiter",
            "Program.cs musí registrovat rate limiter services");
        code.Should().Contain("\"search\"",
            "rate limiter policy 'search' musí existovat");
        code.Should().Contain("AddFixedWindowLimiter",
            "fixed window limiter je vhodný pro per-user burst protection");
        code.Should().Contain("PermitLimit = 30",
            "30 req limit (nesmí být nižší — blokoval by legitimní use)");
        code.Should().Contain("FromSeconds(10)",
            "10s okno");
        code.Should().Contain("Status429TooManyRequests",
            "odmítnuté requesty musí vrátit 429");
    }

    [Fact]
    public void Program_Should_Use_RateLimiter_Middleware_BeforeMapControllers()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Program.cs"));

        code.Should().Contain("app.UseRateLimiter()",
            "UseRateLimiter middleware musí být zaregistrováno");

        var rateLimiterIdx = code.IndexOf("app.UseRateLimiter()", System.StringComparison.Ordinal);
        var mapControllersIdx = code.IndexOf("app.MapControllers()", System.StringComparison.Ordinal);

        rateLimiterIdx.Should().BeGreaterThan(0);
        mapControllersIdx.Should().BeGreaterThan(0);
        rateLimiterIdx.Should().BeLessThan(mapControllersIdx,
            "UseRateLimiter musí být před MapControllers aby [EnableRateLimiting] atributy fungovaly");
    }

    [Fact]
    public void SearchController_Should_Have_EnableRateLimiting_Attribute()
    {
        var attr = typeof(SearchController)
            .GetCustomAttribute<EnableRateLimitingAttribute>(inherit: false);

        attr.Should().NotBeNull(
            "SearchController musí mít [EnableRateLimiting] aby FTS queries byly chráněné");
        attr!.PolicyName.Should().Be("search",
            "atribut musí odkazovat na 'search' policy z Program.cs");
    }
}
