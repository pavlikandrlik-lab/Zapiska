using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using PmTracker.Web.Controllers;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Hledání je dostupné každému přihlášenému (rozhodnutí 2026-10-13). Co uživatel uvidí,
/// omezuje výhradně ProjectVisibilityResolver (spec 2026-09-17 §2.3). Klíč search.index byl
/// zrušen: jako globální klíč ho držely i projektové a subsystémové role, jenže bez projektId
/// se kontroloval jen globálně a hledání vracelo 403 všem kromě globálních rolí (89bb967).
/// </summary>
public sealed class SearchControllerAuthzTests
{
    [Fact]
    public void SearchController_HlidaJenPrihlaseni()
    {
        var authorize = typeof(SearchController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        authorize.Should().ContainSingle();
        authorize[0].Policy.Should().BeNull("o rozsahu výsledků rozhoduje viditelnost projektů, ne klíč");
    }

    [Fact]
    public void Akce_NemajiVlastniPermissionPolicy()
    {
        typeof(SearchController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>())
            .Should().BeEmpty();
    }
}
