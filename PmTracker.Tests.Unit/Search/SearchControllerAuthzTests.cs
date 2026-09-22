using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using PmTracker.Web.Controllers;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// SearchController musí gatovat přístup klíčem search.index (spec 2026-09-17 §5:
/// klíč zůstává a drží ho všech 12 rolí). Guard proti regresi na holé [Authorize],
/// kvůli které byl klíč mrtvý a hledání otevřené každému přihlášenému.
/// </summary>
public sealed class SearchControllerAuthzTests
{
    [Fact]
    public void SearchController_MaPolicySearchIndex()
    {
        var authorize = typeof(SearchController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        authorize.Should().ContainSingle()
            .Which.Policy.Should().Be("permission:search.index");
    }
}
