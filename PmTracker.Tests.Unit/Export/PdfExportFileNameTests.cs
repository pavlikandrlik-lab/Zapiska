using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>Serverové PDF (2026-09-04), spec §6.5: názvy stahovaných dokumentů.</summary>
public sealed class PdfExportFileNameTests
{
    private static readonly DateTime Ctvrty = new(2026, 9, 4);

    private static PdfExportRecordViewModel Record(string cisloViditelne) => new()
    {
        CisloViditelne = cisloViditelne,
        Nazev = "Testovací záznam",
        KategorieKod = "U",
        Kategorie = "Úkol",
        Stav = "Rozpracováno",
        Vlastnik = "Pavel Admin",
        SubsystemKod = "INT",
        Subsystem = "Integrace",
        Spoluprace = Array.Empty<string>(),
        Vyjadreni = Array.Empty<PdfExportCommentViewModel>()
    };

    private static PdfExportTemplateViewModel Model(
        string? zkratka,
        string variant,
        int? jednaniCislo = null,
        string? cisloViditelne = null) => new()
    {
        ProjektNazev = "Ekonomický IS",
        ProjektZkratka = zkratka,
        JednaniStav = "Uzavřeno",
        JednaniCislo = jednaniCislo,
        Vytvoril = "Pavel Admin",
        SnapshotSummary = string.Empty,
        AppliedRuleSummary = Array.Empty<string>(),
        Legenda = Array.Empty<PdfLegendItemViewModel>(),
        Zaznamy = cisloViditelne is null
            ? Array.Empty<PdfExportRecordViewModel>()
            : [Record(cisloViditelne)],
        NormalizedVariant = variant
    };

    [Fact]
    public void Build_NamesProjectExport()
        => PdfExportFileName.Build(Model("EIS", "project_all"), Ctvrty)
            .Should().Be("Zapiska_EIS_projekt_2026-09-04.pdf");

    [Fact]
    public void Build_NamesMeetingByNumber()
        => PdfExportFileName.Build(Model("EIS", "meeting", jednaniCislo: 12), Ctvrty)
            .Should().Be("Zapiska_EIS_jednani-12_2026-09-04.pdf");

    [Fact]
    public void Build_NamesTaskByVisibleRecordNumber()
        => PdfExportFileName.Build(Model("EIS", "task_single", cisloViditelne: "901-1"), Ctvrty)
            .Should().Be("Zapiska_EIS_zaznam-901-1_2026-09-04.pdf");

    [Fact]
    public void Build_ReplacesSeparatorsAndSpaces()
        => PdfExportFileName.Build(Model("EIS/ACR 2", "project_all"), Ctvrty)
            .Should().Be("Zapiska_EIS_ACR_2_projekt_2026-09-04.pdf");

    [Fact]
    public void Build_KeepsCzechDiacritics()
        => PdfExportFileName.Build(Model("Zápiska", "project_all"), Ctvrty)
            .Should().Contain("Zápiska",
                "diakritika je v názvu souboru platná a přenese se hlavičkou filename*");

    [Fact]
    public void Build_FallsBackWhenProjectCodeMissing()
        => PdfExportFileName.Build(Model(null, "project_all"), Ctvrty)
            .Should().Be("Zapiska_Projekt_projekt_2026-09-04.pdf");

    [Fact]
    public void Build_FallsBackWhenTaskHasNoRecord()
        => PdfExportFileName.Build(Model("EIS", "task_single"), Ctvrty)
            .Should().Be("Zapiska_EIS_zaznam-0_2026-09-04.pdf",
                "prázdný export úkolu nesmí vyrobit rozbitý název");
}
