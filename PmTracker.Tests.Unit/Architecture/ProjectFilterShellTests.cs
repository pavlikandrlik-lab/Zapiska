using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec: docs/superpowers/specs/2026-04-30-project-filter-unification-design.md.
/// Architecture invariants pro sjednocený project filter (Záznamy + Harmonogram):
///   1) Existuje jediný partial _ProjectFilterShell.cshtml (DRY single source of filter markup).
///   2) Partial obsahuje všech 10 filter polí (data-filter-key atributů).
///   3) Oba taby (_ProjectRecordsTab + _ProjectScheduleTab) volají PartialAsync(_ProjectFilterShell).
///   4) projectFilter.js obsahuje migrateLegacyProjectFilterStorageKeys helper.
///   5) projectFilter.js používá výhradně data-filter-* (žádné data-schedule-filter-* references).
/// </summary>
public sealed class ProjectFilterShellTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Fact]
    public void Partial_ContainsAllTenFilterKeys()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml");

        var requiredKeys = new[]
        {
            "groupBySubsystem", "subsystem", "sortBy", "kategorie", "stav",
            "typ", "vlastnik", "aktivni", "mine", "jednani-vyjadreni-stav"
        };
        foreach (var key in requiredKeys)
        {
            var pattern = $@"data-filter-key=""{Regex.Escape(key)}""";
            Regex.IsMatch(src, pattern).Should().BeTrue(
                $"_ProjectFilterShell musí obsahovat data-filter-key=\"{key}\" (10-field parity).");
        }
    }

    [Fact]
    public void RecordsTab_DelegatesFilterMarkupToSharedPartial()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml");
        Regex.IsMatch(src, @"PartialAsync\(""_ProjectFilterShell""")
            .Should().BeTrue("_ProjectRecordsTab musí volat PartialAsync na shared filter shell (DRY).");
    }

    [Fact]
    public void ScheduleTab_DelegatesFilterMarkupToSharedPartial()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml");
        Regex.IsMatch(src, @"PartialAsync\(""_ProjectFilterShell""")
            .Should().BeTrue("_ProjectScheduleTab musí volat PartialAsync na shared filter shell (DRY).");
    }

    [Fact]
    public void ScheduleTab_DoesNotUseLegacyDataScheduleFilterKey()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml");
        Regex.IsMatch(src, @"data-schedule-filter-key")
            .Should().BeFalse("_ProjectScheduleTab nesmí používat legacy data-schedule-filter-key (sjednoceno na data-filter-key).");
    }

    [Fact]
    public void ProjectFilterJs_ContainsLegacyStorageMigration()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
        src.Should().Contain("migrateLegacyProjectFilterStorageKeys",
            "projectFilter.js musí obsahovat one-time migration helper pro legacy .records/.schedule storage keys.");
    }

    [Fact]
    public void ProjectFilterJs_DoesNotReferenceDataScheduleFilterKey()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
        src.Should().NotContain("data-schedule-filter",
            "projectFilter.js musí používat výhradně data-filter-* (sjednoceno 2026-04-30).");
    }

    [Fact]
    public void FilterIndexJs_ContainsTabSyncListener()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/filters/index.js");
        src.Should().Contain("initProjectFilterTabSync",
            "filters/index.js musí exportovat initProjectFilterTabSync (pm-tab-change listener).");
        src.Should().Contain("\"pm-tab-change\"",
            "filters/index.js musí poslouchat pm-tab-change event z pm-tabs Web Component.");
    }
}
