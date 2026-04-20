using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3B Task 1: recordEditor.js (1919 LOC, 57 exports) rozdělen
/// do 4 feature modulů + orchestrator + backward-compat barrel re-export.
/// </summary>
public sealed class RecordEditorJsSplitTests
{

    [Theory]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/index.js")]
    public void Submodule_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3B Task 1");
        File.ReadAllText(full).Length.Should().BeGreaterThan(500, "každý submodul má smysluplný obsah");
    }

    [Fact]
    public void BarrelModule_ShouldReExportFromSubmodule()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor.js"));
        content.Should().Contain("export * from \"./recordEditor/index.js\"",
            "recordEditor.js zachovává backward-compat re-export barrel");
        File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor.js")).Length
            .Should().BeLessThan(50, "barrel je jen re-export");
    }

    [Fact]
    public void NavigationModule_ShouldContainOpenRecordEditor()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js"));
        content.Should().Contain("export function openRecordEditor");
        content.Should().Contain("export const recordEditorState");
        content.Should().NotContain("function initRichTextEditors", "richtext patří do richtext.js");
        content.Should().NotContain("function saveRecordEditorDraft", "draft patří do draft.js");
    }

    [Fact]
    public void DraftModule_ShouldContainDraftFunctions()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js"));
        content.Should().Contain("saveRecordEditorDraft");
        content.Should().Contain("readRecordEditorDraft");
        content.Should().Contain("isRecordEditorFormDirty");
        content.Should().Contain("promptRecordEditorDiscard");
        content.Should().Contain("markRecordEditorFormClean");
        content.Should().Contain("requestRecordEditorModalClose",
            "requestRecordEditorModalClose je v draft.js (close-guard logika, bez circular dep)");
        content.Should().NotContain("initRichTextEditors", "richtext patří do richtext.js");
    }

    [Fact]
    public void RichtextModule_ShouldContainQuillInit()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js"));
        content.Should().Contain("initRichTextEditors");
        content.Should().Contain("looksLikeHtml");
        content.Should().Contain("setRecordEditorRichTextValue");
    }

    [Fact]
    public void FormModule_ShouldContainMetadataBindings()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js"));
        content.Should().Contain("updateTaskTypeVisibility");
        content.Should().Contain("initRecordFormTabs");
        content.Should().Contain("normalizeServerFieldKey");
        content.Should().NotContain("saveRecordEditorDraft", "draft patří do draft.js");
    }

    [Fact]
    public void IndexModule_ShouldOrchestrate()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/index.js"));
        content.Should().Contain("initRecordFormEnhancements",
            "orchestrator exportuje inicializační entry point");
        content.Should().Contain("export",
            "index.js re-exportuje public API submodules");
    }

    [Fact]
    public void Bundle_ShouldContainKeyExports()
    {
        var bundle = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/site.bundle.js"));
        bundle.Should().Contain("openRecordEditor",
            "bundle obsahuje klíčovou funkci z recordEditor/navigation");
        bundle.Should().Contain("initRichTextEditors",
            "bundle obsahuje klíčovou funkci z recordEditor/richtext");
        bundle.Should().Contain("isRecordEditorFormDirty",
            "bundle obsahuje klíčovou funkci z recordEditor/draft");
        bundle.Should().Contain("initRecordFormEnhancements",
            "bundle obsahuje orchestrator");
    }
}
