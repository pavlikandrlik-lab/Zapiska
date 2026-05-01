using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// Pre-delete confirm dialog (DeleteRecordModal) musí zobrazit kompletní souhrn
/// toho, co se společně se záznamem trvale smaže. User požadavek 2026-04-27:
/// nezůstávat data v DB, uživatel musí vědět, kolik toho mizí.
///
/// Modal byl rozšířen o 4 nové counts:
///   - HarvestVyjadreniCount (vyjadreni_vazby — bývá řádově vyšší než VyjadreniCount)
///   - NavrhyTargetCount (proposals targeting tento záznam)
///   - NavrhyOriginCount (proposals které tento záznam vytvořily — SET NULL preserve)
///   - HistorieCount (sum z 6 historie tabulek)
/// </summary>
public sealed class DeleteRecordModalCountsTests
{
    private static string LoadFile(string relativePath)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }

        var path = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"soubor musí existovat: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void DeleteRecordModalViewModel_MustExposeNewCounts()
    {
        var source = LoadFile("PmTracker.Web/Models/ViewModels/ModalViewModels.cs");

        // Klíčové: po DELETE projektove_zaznamy SQL CASCADE smaže VŠECHNY tyto
        // entity. User je proto musí vidět v confirmation modalu.
        source.Should().Contain("HarvestVyjadreniCount",
            "Modal musí ukázat počet harvestnutých vyjádření (zaznam_harmonogram_vyjadreni_vazba).");
        source.Should().Contain("NavrhyTargetCount",
            "Modal musí ukázat počet návrhů úprav cílených na záznam (zaznam_id).");
        source.Should().Contain("NavrhyOriginCount",
            "Modal musí ukázat počet návrhů, které záznam vytvořily (approved_record_id) — SET NULL preserve.");
        source.Should().Contain("HistorieCount",
            "Modal musí ukázat souhrn historie záznamů (6 zaznam_historie_* tabulek).");
    }

    [Fact]
    public void EditorQueries_MustPopulateNewCountsFromDb()
    {
        var source = LoadFile("PmTracker.Web/Services/RecordService.EditorQueries.cs");

        // Composer musí volat odpovídající Count() queries
        source.Should().Contain("VyjadreniVazby.AsNoTracking().CountAsync",
            "Composer musí počítat harvestnutá vyjádření z VyjadreniVazby tabulky.");
        source.Should().MatchRegex(@"ZaznamNavrhy\.AsNoTracking\(\)\.CountAsync\(\s*x\s*=>\s*x\.ZaznamId",
            "Composer musí počítat proposals targeting (ZaznamId).");
        source.Should().MatchRegex(@"ZaznamNavrhy\.AsNoTracking\(\)\.CountAsync\(\s*x\s*=>\s*x\.ApprovedRecordId",
            "Composer musí počítat proposals origin (ApprovedRecordId).");

        // Historie sum (6 tabulek)
        source.Should().Contain("ZaznamHistorieZmenTypu.AsNoTracking().CountAsync");
        source.Should().Contain("ZaznamHistorieTerminu.AsNoTracking().CountAsync");
        source.Should().Contain("ZaznamHistorieVlastnik.AsNoTracking().CountAsync");
        source.Should().Contain("ZaznamHistorieSubsystem.AsNoTracking().CountAsync");
        source.Should().Contain("ZaznamHistorieStavuZaznamu.AsNoTracking().CountAsync");
        source.Should().Contain("ZaznamHistorieStavuProjektu.AsNoTracking().CountAsync");

        // Property assignments
        source.Should().Contain("HarvestVyjadreniCount = harvestCount");
        source.Should().Contain("NavrhyTargetCount = navrhyTargetCount");
        source.Should().Contain("NavrhyOriginCount = navrhyOriginCount");
        source.Should().Contain("HistorieCount = historieCount");
    }

    [Fact]
    public void DeleteRecordModal_RazorView_MustDisplayNewCounts()
    {
        var view = LoadFile("PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml");

        view.Should().Contain("HarvestVyjadreniCount",
            "Razor view musí binovat HarvestVyjadreniCount.");
        view.Should().Contain("NavrhyTargetCount",
            "Razor view musí binovat NavrhyTargetCount.");
        view.Should().Contain("HistorieCount",
            "Razor view musí binovat HistorieCount.");

        // User-facing labels (česky)
        view.Should().Contain("Harvestnutá vyjádření ze ServiceDesku",
            "User-facing label pro harvest count musí být srozumitelný (ServiceDesk kontext).");
        view.Should().Contain("Návrhy úprav",
            "User-facing label pro proposals count musí obsahovat 'Návrhy úprav'.");
        view.Should().Contain("Záznamy historie změn",
            "User-facing label pro historie count musí obsahovat 'Záznamy historie změn'.");
    }
}
