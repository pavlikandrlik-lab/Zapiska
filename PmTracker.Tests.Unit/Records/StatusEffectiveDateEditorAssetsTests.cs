using System.IO;
using FluentAssertions;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-13 §6 — napojení volby „Změna stavu platí k“ v editoru.</summary>
public sealed class StatusEffectiveDateEditorAssetsTests
{
    [Fact]
    public void Css_HiddenFormRow_IsReallyHidden()
        => File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"))
            .Should().Contain(".record-form-row[hidden]",
                ".record-form-row má display:grid, který atribut hidden jinak přebije");

    [Fact]
    public void EditorBootstrap_InitializesStatusEffectiveDate()
    {
        var index = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/index.js"));

        index.Should().Contain("from \"./statusEffectiveDate.js\"");
        index.Should().Contain("initStatusEffectiveDate(scope);",
            "editor se vkládá i dodatečně (lazy load, refresh karty) a vždy přes initRecordFormEnhancements");
    }

    [Fact]
    public void BasicPanel_RendersChoiceOnlyWhenMetadataIsEditable()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml"));

        view.Should().Contain("Model.StavPlatiK is not null && !metadataLocked");
        view.Should().Contain("name=\"StavPlatiK\"");
        view.Should().Contain("data-original-stav=\"@Model.Stav\"");
    }
}
