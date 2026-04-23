using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class NewPermissionKeysSeedTests
{
    [Theory]
    [InlineData("dashboard.view")]
    [InlineData("export.pdf.projekt")]
    [InlineData("export.word.projekt")]
    [InlineData("comments.add")]
    [InlineData("comments.edit.own")]
    [InlineData("comments.delete.own")]
    [InlineData("search.reindex")]
    [InlineData("projects.read.all")]
    public void NewKey_ShouldBeInPermissionKeysSupportedSet(string key)
    {
        PermissionKeys.IsSupported(key).Should().BeTrue(
            $"permission key '{key}' musí být registrovaný v PermissionKeys.Definitions");
    }

    [Theory]
    [InlineData("dashboard.view")]
    [InlineData("export.pdf.projekt")]
    [InlineData("export.word.projekt")]
    [InlineData("comments.add")]
    [InlineData("comments.edit.own")]
    [InlineData("comments.delete.own")]
    [InlineData("search.reindex")]
    [InlineData("projects.read.all")]
    public void NewKey_ShouldBeSeededInActionsList(string key)
    {
        PermissionSeedConfiguration.Actions
            .Select(a => a.Klic)
            .Should().Contain(key,
                $"permission key '{key}' musí být v seedu Actions");
    }

    [Fact]
    public void ProjectScope_NewKeys_ShouldBeProjectScopeLevel()
    {
        foreach (var key in new[] { "dashboard.view", "export.pdf.projekt", "export.word.projekt", "comments.add", "comments.edit.own", "comments.delete.own" })
        {
            var action = PermissionSeedConfiguration.Actions.First(a => a.Klic == key);
            action.ScopeLevel.Should().Be(PermissionScopeLevel.Project,
                $"{key} potřebuje projektový kontext");
        }
    }

    [Fact]
    public void GlobalScope_NewKeys_ShouldBeGlobalScopeLevel()
    {
        foreach (var key in new[] { "search.reindex", "projects.read.all" })
        {
            var action = PermissionSeedConfiguration.Actions.First(a => a.Klic == key);
            action.ScopeLevel.Should().Be(PermissionScopeLevel.Global,
                $"{key} je globální — nepotřebuje projektId");
        }
    }
}
