using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #5 (2026-04-20): News items obsahují ActorName + kontext změny
/// (co/kdo). Současná Description redundantně opakuje ProjectLabel → nahrazena
/// smysluplným kontextem per event type.
/// </summary>
public sealed class DashboardNewsEnrichmentTests
{
    [Fact]
    public void NewsItemViewModel_ShouldHaveActorNameField()
    {
        var vmSrc = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/HomeViewModels.cs"));
        vmSrc.Should().Contain("public string ActorName",
            "DashboardNewsItemViewModel musí mít ActorName field");
    }

    [Fact]
    public void DashboardService_ShouldPopulateActorName()
    {
        var svcSrc = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardService.cs"));
        svcSrc.Should().Contain("ActorName = ",
            "BuildNewsItemsAsync musí populovat ActorName na každém item");
    }

    [Fact]
    public void DashboardService_ShouldNotUseRedundantProjectLabelInDescription()
    {
        var svcSrc = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardService.cs"));
        // Staré: Description = $"Záznam v projektu {BuildProjectLabel(...)}" — redundantní,
        // nové: Description obsahuje actor + kontext
        svcSrc.Should().NotContain("Description = $\"Záznam v projektu",
            "Description nesmí redundantně opakovat projekt (ProjectLabel je separate slot)");
    }

    [Fact]
    public void NewsListPartial_ShouldRenderActorInMeta()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml"));
        view.Should().Contain("@item.ActorName",
            "partial musí renderovat ActorName");
    }
}
