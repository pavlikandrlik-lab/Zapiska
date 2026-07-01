using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Pokrývá redesign návrhů 7a–7e (docs/superpowers/specs/2026-06-17-navrhy-redesign-design.md).
/// File-text asserce nad views / JS / službou — drží gating logiku konzistentní.
/// </summary>
public sealed class NavrhyRedesignTests
{
    private static string Load(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("kořen repozitáře (PmTracker.sln) musí existovat");
        var full = Path.Combine(directory!.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat: {full}");
        return File.ReadAllText(full);
    }

    // ---- 7a: rozhodovací akce jen v detailu, panel jen „Detail návrhu" ----

    [Fact]
    public void Panel_ShouldNotContainDecisionForms()
    {
        var panel = Load("PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml");
        panel.Should().NotContain("asp-action=\"ApproveProposal\"",
            "7a: rozhodovací akce (Schválit) jsou jen v detailu, ne na panelu");
        panel.Should().NotContain("asp-action=\"RejectProposal\"",
            "7a: Zamítnout je jen v detailu");
        panel.Should().NotContain("asp-action=\"RejectAndTakeOverCreateProposal\"",
            "7a: Zamítnout a převzít data je jen v detailu");
        panel.Should().NotContain("asp-action=\"RejectAndEditProposal\"",
            "7a: Zamítnout a upravit je jen v detailu");
        panel.Should().Contain("Detail návrhu", "7a: panel ponechává tlačítko Detail návrhu");
    }

    [Fact]
    public void DetailFooter_ShouldContainRejectAndTakeOverForCreateProposal()
    {
        var form = Load("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml");
        form.Should().Contain("RejectAndTakeOverCreateProposal",
            "7a: detail návrhu založení má „Zamítnout a převzít data\"");
        form.Should().Contain("Model.CanRejectAndTakeOverProposal",
            "7a: tlačítko je gateované flagem CanRejectAndTakeOverProposal");
    }

    [Fact]
    public void ProposalDetailEditor_ShouldSplitRejectVariantByType()
    {
        var queries = Load("PmTracker.Web/Services/RecordProposalService.Queries.cs");
        queries.Should().Contain("model.CanRejectAndTakeOverProposal = canDecide && isPending && isCreateProposal;",
            "7a: Zamítnout a převzít data jen pro návrh založení");
        queries.Should().Contain("model.CanRejectAndEditProposal = canDecide && isPending && !isCreateProposal;",
            "7a: Zamítnout a upravit jen pro návrh harmonogramu");
    }

    // ---- 7b: harmonogram v návrhu založení = plán-only (skutečnost skrytá) ----

    [Fact]
    public void ScheduleBlock_ShouldHideActualWhenFlagSet()
    {
        var block = Load("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml");
        block.Should().Contain("Model.HideActual", "7b: skutečnostní sloupec se skrývá přes HideActual");
        block.Should().Contain("Vyplní se po založení",
            "7b: místo skutečnostního vstupu se zobrazí poznámka");
    }

    [Fact]
    public void CreateProposalEditor_ShouldHideScheduleActual()
    {
        var queries = Load("PmTracker.Web/Services/RecordProposalService.Queries.cs");
        queries.Should().Contain("model.HarmonogramBlok.HideActual = true;",
            "7b: návrh založení nastaví HideActual=true");
    }

    // ---- 7c: externí vazby v návrhu = jen čísla, žádný harvest ----

    [Fact]
    public void ExternalPanel_ShouldGateHarvestUiOnFlag()
    {
        var panel = Load("PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml");
        panel.Should().Contain("Model.HideExternalHarvestUi",
            "7c: harvest UI (chat + 4 datumy) gateované flagem HideExternalHarvestUi");
    }

    [Fact]
    public void CreateProposalEditor_ShouldHideExternalHarvestUi()
    {
        var queries = Load("PmTracker.Web/Services/RecordProposalService.Queries.cs");
        queries.Should().Contain("model.HideExternalHarvestUi = true;",
            "7c: návrh založení skryje harvest UI externího panelu");
    }

    [Fact]
    public void ExternalSyncJs_ShouldSkipProposalEditor()
    {
        var sync = Load("PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js");
        sync.Should().Contain("data-is-proposal-editor=\"true\"",
            "7c: sync (SD preview) se v editoru návrhu přeskočí — netěžit ani nezkoumat obsah");
    }

    // ---- 7d: harmonogram tab v create editoru vždy ----

    [Fact]
    public void EditorForm_ShouldRenderScheduleTabInCreateEditors()
    {
        var form = Load("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml");
        form.Should().Contain("Model.JeUkolKategorie || Model.IsCreate",
            "7d: harmonogram tab/panel se v create editorech (klasik i návrh) renderuje vždy");
    }

    // ---- 7e: tlačítka u sebe, poznámka pod čarou ----

    [Fact]
    public void EditorFooter_ShouldGroupButtonsAndFootnote()
    {
        var form = Load("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml");
        form.Should().Contain("record-editor-actions-buttons",
            "7e: akční tlačítka jsou v jednom řádku u sebe");
        form.Should().Contain("record-editor-footnote",
            "7e: vysvětlující text je pod tlačítky jako poznámka pod čarou");
    }
}
