using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Pokrývá specifikaci docs/specs/record-proposal-editor.md.
///
/// V editoru návrhu úpravy harmonogramu (proposal editor) smí uživatel
/// měnit POUZE termín ukončení a harmonogram. Metadata záznamu (kategorie,
/// typ úkolu, stav, subsystém, vlastník) musí být zamčená na serveru
/// i na klientovi. Klient NESMÍ odemknout pole, které server zamkl.
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
        var source = LoadText("PmTracker.Web/wwwroot/js/modules/recordEditor.js");

        source.Should().Contain(
            "form.dataset.metadataLocked === \"true\"",
            "JS musí číst zámek z data-metadata-locked");
        source.Should().Contain(
            "typeSelect.disabled = !isTask || metadataLocked;",
            "TypUkolu select musí zůstat disabled pokud server zamkl metadata, i pro kategorii Úkol");
    }

    [Fact]
    public void BasicPanel_ShouldDisableMetadataFieldsWhenLocked()
    {
        var source = LoadText("PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml");

        // Server-side zámek musí zůstat
        source.Should().Contain(
            "var metadataLocked = !Model.AllowBasicMetadataEdit;",
            "panel musí odvozovat metadataLocked z AllowBasicMetadataEdit");

        // TypUkolu select disable
        source.Should().MatchRegex(
            "name=\"TypUkolu\"[^>]*@\\(metadataLocked \\? \"disabled=\\\\\"disabled\\\\\"\" : null\\)",
            "select TypUkolu musí respektovat metadataLocked");
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
