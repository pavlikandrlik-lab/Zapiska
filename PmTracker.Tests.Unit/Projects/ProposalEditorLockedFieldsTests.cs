using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Pokrývá specifikaci docs/specs/record-proposal-editor.md.
///
/// V editoru návrhu ZMĚNY termínu a harmonogramu smí uživatel měnit POUZE termín
/// ukončení a harmonogram — metadata (kategorie, typ úkolu, stav, subsystém, vlastník)
/// jsou zamčená (AllowBasicMetadataEdit=false → metadataLocked=true). Klient NESMÍ
/// odemknout pole, které server zamkl.
///
/// 7d (2026-06-17): zámek typu úkolu řídí VÝHRADNĚ metadataLocked. Návrh ZALOŽENÍ
/// (a klasický create/edit) má metadataLocked=false → typ úkolu je editovatelný při
/// kategorii úkol. Dřívější proposal-specifický zámek (isProposalEditor) byl odstraněn.
/// </summary>
public sealed class ProposalEditorLockedFieldsTests
{
    private static string LoadText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void EditZaznamForm_ShouldSetDataMetadataLockedAttribute()
    {
        var source = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml");

        source.Should().Contain(
            "data-metadata-locked=\"@(Model.AllowBasicMetadataEdit ? \"false\" : \"true\")\"",
            "formulář musí propagovat server-side zámek metadat do DOM atributu");
        source.Should().Contain(
            "data-is-proposal-editor=\"@(Model.IsProposalEditor ? \"true\" : \"false\")\"",
            "formulář musí indikovat proposal mód pro JS");
    }

    [Fact]
    public void RecordEditorJs_ShouldRespectMetadataLockedInTaskTypeVisibility()
    {
        // Fáze 3B Task 1: recordEditor.js je barrel — updateTaskTypeVisibility
        // je v submodulu recordEditor/form.js
        var source = LoadText("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js");

        source.Should().Contain(
            "form.dataset.metadataLocked === \"true\"",
            "JS musí číst zámek z data-metadata-locked");
        // 7d (2026-06-17): typ úkolu se zamyká VÝHRADNĚ přes metadataLocked. Návrh ZMĚNY
        // harmonogramu má metadataLocked=true → zamčeno; návrh ZALOŽENÍ i klasik mají
        // metadataLocked=false → typ je editovatelný při kategorii úkol.
        source.Should().Contain(
            "typeSelect.disabled = !isTask || metadataLocked;",
            "TypUkolu je disabled jen když není úkol nebo jsou metadata zamčená (ne kvůli proposal módu)");
        source.Should().NotContain(
            "!isTask || metadataLocked || isProposalEditor",
            "proposal-specifický zámek typu úkolu byl v 7d odstraněn — řídí jen metadataLocked");
    }

    [Fact]
    public void BasicPanel_ShouldDisableMetadataFieldsWhenLocked()
    {
        var source = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml");

        // Server-side zámek musí zůstat
        source.Should().Contain(
            "var metadataLocked = !Model.AllowBasicMetadataEdit;",
            "panel musí odvozovat metadataLocked z AllowBasicMetadataEdit");

        // 7d (2026-06-17): TypUkolu select disable řídí JEN metadataLocked (ne IsProposalEditor) —
        // návrh založení i klasik povolí typ při kategorii úkol; návrh změny harmonogramu má
        // metadataLocked=true (basic pole zamčená).
        source.Should().MatchRegex(
            "name=\"TypUkolu\"[^>]*metadataLocked[^>]*disabled",
            "select TypUkolu musí respektovat metadataLocked");
        source.Should().NotContain(
            "metadataLocked || Model.IsProposalEditor",
            "proposal-specifický zámek typu úkolu byl v 7d odstraněn");
    }

    /// <summary>
    /// Regrese 2026-06-29: návrh ZMĚNY termínu a harmonogramu (AllowBasicMetadataEdit=false
    /// → metadataLocked=true) renderoval Kategorie/Stav/Nazev/Subsystem jako disabled
    /// &lt;select&gt;/&lt;input&gt;. Disabled prvky se s formulářem NEodesílají, ale
    /// SaveRecordCommand je má [Required] → POST /Navrhy/SubmitScheduleProposal spadl na
    /// REQUEST_VALIDATION_FAILED (Kategorie/Nazev/Stav/Subsystem required).
    ///
    /// Oprava (stejný princip jako VlastnikId hidden input a _AppDateField locked): při
    /// zámku metadat panel pošle jejich hodnoty hidden inputy, aby validace prošla. Hidden
    /// inputy MUSÍ být pod podmínkou metadataLocked — jinak by v create/edit (pole povolená)
    /// odeslaly stejný name dvakrát.
    /// </summary>
    [Fact]
    public void BasicPanel_ShouldSubmitLockedRequiredMetadata_WhenMetadataLocked()
    {
        var source = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml");

        source.Should().MatchRegex(
            @"@if\s*\(\s*metadataLocked\s*\)",
            "hidden carriers pro zamčená metadata musí být pod podmínkou metadataLocked (jinak dvojité odeslání v create/edit)");

        foreach (var field in new[] { "Kategorie", "Stav", "Nazev", "Subsystem" })
        {
            source.Should().Contain(
                $"<input type=\"hidden\" name=\"{field}\"",
                $"zamčené [Required] pole {field} se musí odeslat hidden inputem, jinak SaveRecordCommand spadne na [Required] validaci");
        }
    }

    [Fact]
    public void SpecDocument_ShouldExist()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        var specPath = Path.Combine(directory!.FullName, "docs", "specs", "record-proposal-editor.md");
        File.Exists(specPath).Should().BeTrue(
            $"specifikace musí existovat na {specPath}");
    }
}
