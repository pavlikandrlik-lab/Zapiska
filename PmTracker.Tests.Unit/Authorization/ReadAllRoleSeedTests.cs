using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ReadAllRoleSeedTests
{
    [Fact]
    public void READ_ALL_Role_ShouldBeSeeded()
    {
        PermissionSeedConfiguration.Roles
            .Should().ContainSingle(r => r.Kod == "READ_ALL");
    }

    [Fact]
    public void READ_ALL_Role_ShouldBeGlobalScope()
    {
        var role = PermissionSeedConfiguration.Roles.First(r => r.Kod == "READ_ALL");
        role.Scope.Should().Be(RoleScope.Global);
        role.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void READ_ALL_Role_ShouldGrantReadingAcrossAllProjects()
    {
        var permissions = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == "READ_ALL" && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

        // Per-action redesign (F7 2026-04-23): READ_ALL má 13 cílových klíčů, deprecated smazány.
        permissions.Should().BeEquivalentTo(new[]
        {
            "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
            "dashboard.view", "dashboard.vyzvy.view",
            "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
            "export.word.jednani", "export.word.projekt", "export.word.ukol",
            "projects.read.all",
            "search.index"
        }, "READ_ALL má read + per-entita export + search.index + projects.read.all");
    }
}
