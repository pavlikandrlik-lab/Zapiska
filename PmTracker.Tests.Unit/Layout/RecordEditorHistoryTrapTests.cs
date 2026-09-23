using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A8 (2026-07-08): pushState-trap pro šipku zpět na stránkovém editoru.
/// Sentinel entry + popstate → aplikační dialog (promptRecordEditorDiscard);
/// beforeunload zůstává prázdný (žádný browser dialog).
/// </summary>
public sealed class RecordEditorHistoryTrapTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    [Fact]
    public void HistoryTrap_Exists_UsesDirtyCheck_AndDiscardPrompt()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js");
        src.Should().Contain("export function initRecordEditorHistoryTrap");
        src.Should().Contain("pmEditorTrap");
        src.Should().Contain("popstate");
        src.Should().Contain("isRecordEditorFormDirty");
        src.Should().Contain("promptRecordEditorDiscard");
        src.Should().NotContain("beforeunload", "nativní dialog je zakázaný");
    }

    [Fact]
    public void Bootstrap_WiresHistoryTrap()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("initRecordEditorHistoryTrap");
    }

    [Fact]
    public void GuardOff_IsRespected_ByTrapAndDiscardPrompt()
    {
        Read("PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js")
            .Should().Contain("recordEditorGuard === \"off\"");
        Read("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js")
            .Should().Contain("recordEditorGuard === \"off\"");
    }

    [Fact]
    public void ScheduleTabActivation_MergesBaseline_InsteadOfFullRebuild()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js");
        src.Should().Contain("mergeScheduleKeysIntoBaseline",
            "B2: plný rebuild baseline absorboval user změny → merge jen schedule klíčů");
        src.Should().Contain("scheduleSnapshotKeyPrefixes");
    }
}
