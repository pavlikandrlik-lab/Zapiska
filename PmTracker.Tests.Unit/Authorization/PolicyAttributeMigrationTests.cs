using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Ověřuje, že v Phase C fixed security hole controllerech žádná action
/// už NEMÁ body-level HasPermission check — vše je přes [Authorize(Policy=...)].
/// </summary>
public sealed class PolicyAttributeMigrationTests
{
    [Fact]
    public void ExportController_ShouldUseAuthorizePolicyAttributes()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:export.pdf\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:export.word\")]");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.ExportPdf",
            "body check musí být nahrazen [Authorize(Policy)] atributem");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.ExportWord",
            "body check musí být nahrazen [Authorize(Policy)] atributem");
    }

    [Fact]
    public void ProjectDashboardController_ShouldUseAuthorizePolicyAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjectDashboardController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:dashboard.view\")]");
        code.Should().NotContain("CurrentUserContext.HasPermission(PermissionKeys.DashboardView",
            "body check musí být nahrazen [Authorize(Policy)] atributem");
    }

    [Fact]
    public void ZaznamyCommandsController_ShouldUseAuthorizePolicyAttributes()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ZaznamyController.Commands.cs"));

        code.Should().Contain("\"permission:comments.add\"");
        code.Should().Contain("\"permission:comments.edit.own\"");
        code.Should().Contain("\"permission:comments.delete.own\"");
        code.Should().NotContain("hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.CommentsAdd",
            "body hasPermission callback musí být nahrazen [Authorize(Policy)] atributem");
    }

    [Fact]
    public void SearchController_ShouldUseAuthorizePolicyAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/SearchController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:search.reindex\")]");
        code.Should().NotContain("if (!CurrentUserContext.HasPermission(PermissionKeys.SearchReindex",
            "body check musí být nahrazen [Authorize(Policy)] atributem");
    }
}
