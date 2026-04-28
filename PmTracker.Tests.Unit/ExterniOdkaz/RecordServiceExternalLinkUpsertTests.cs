using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// Regression test pro bug 2026-04-27: smazání externí vazby s harvestnutými
/// vyjádřeními vracelo 400 UNEXPECTED_SERVER_ERROR.
///
/// Root cause: <c>ReplaceRecordExternalLinksAsync</c> dělal naive
/// <c>RemoveRange(existing) + Add(new)</c>. Když existující vazba měla
/// harvestnuté vyjádření v <c>zaznam_harmonogram_vyjadreni_vazba</c> (FK
/// <c>NO ACTION</c> per <c>db_upgrade_1_3_6_vyjadreni_vazba.sql</c>), DELETE
/// selhal SQL FK violation → <c>DbUpdateException</c> → catch-all v
/// <c>BuildAjaxExceptionResult</c> → user dostal generic UNEXPECTED_SERVER_ERROR.
///
/// Fix: UPSERT logika — UPDATE existing rows in place (Id zachován → FK refs
/// platné), DELETE jen ty nepřítomné v command, pre-flight zablokovat smazání
/// vazeb s harvestnutými vyjádřeními (vrátit RecordValidationException).
/// </summary>
public sealed class RecordServiceExternalLinkUpsertTests
{
    private static string LoadServiceSource()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var path = Path.Combine(dir.FullName, "PmTracker.Web", "Services", "RecordService.SaveRecord.cs");
        File.Exists(path).Should().BeTrue($"soubor musí existovat na cestě {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void ReplaceRecordExternalLinksAsync_MustNotUseNaiveRemoveRange()
    {
        // Naive pattern: blanket RemoveRange(existing) bez Id-matching způsoboval
        // FK violation pro vazby s harvestnutými vyjádřeními (zaznam_harmonogram_vyjadreni_vazba).
        var source = LoadServiceSource();

        // Najít metodu Replace
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        methodIndex.Should().BeGreaterThan(0, "Metoda ReplaceRecordExternalLinksAsync musí existovat.");

        // Slice = tělo metody (do konce souboru, dostatečně velké okno)
        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        // Anti-pattern: ToListAsync(ct); ... RemoveRange(existing) bez Id-matching
        var naiveBulkRemovePattern = new Regex(
            @"\.ToListAsync\([^)]+\)\s*;\s*dbContext\.ZaznamExterniOdkazy\.RemoveRange\(existing\)\s*;",
            RegexOptions.Singleline);
        naiveBulkRemovePattern.IsMatch(methodSlice).Should().BeFalse(
            "Naive RemoveRange(existing) je zakázán — způsoboval bug 2026-04-27 " +
            "UNEXPECTED_SERVER_ERROR při smazání vazby s harvestnutými vyjádřeními. " +
            "Použij UPSERT: UPDATE in place podle Id, DELETE jen toDelete (rows mimo command).");
    }

    [Fact]
    public void ReplaceRecordExternalLinksAsync_MustImplementUpsertById()
    {
        var source = LoadServiceSource();
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        methodSlice.Should().Contain("commandKeptIds",
            "UPSERT vyžaduje set Id z command (commandKeptIds) pro identifikaci rows k zachování.");
        methodSlice.Should().Contain("existingById",
            "UPSERT vyžaduje dictionary existing rows by Id pro UPDATE-in-place.");
        methodSlice.Should().MatchRegex(@"existingById\.TryGetValue\(\s*link\.Id",
            "UPDATE in place: match existing entity podle command.Id, neměnit Id (FK refs zůstanou platné).");
    }

    [Fact]
    public void ReplaceRecordExternalLinksAsync_MustNotBlockDeletionOfHarvestedLinks()
    {
        // 2026-04-28 reversal — design rozhodnutí:
        // Delete externí vazby je běžná operace a nesmí být blokována pre-flight check.
        // SQL CASCADE (FK_zhvv_externi_odkaz, db_upgrade_1_3_13_externi_odkaz_cascade.sql)
        // automaticky čistí navázané vyjadreni_vazby rows.
        //
        // Pre-flight harvest_locked check byl ODSTRANĚN. RecordValidationException
        // s rule "external_link_harvest_locked" se NESMÍ v této metodě vyskytovat.
        var source = LoadServiceSource();
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        methodSlice.Should().NotContain("external_link_harvest_locked",
            "Pre-flight check 'external_link_harvest_locked' byl odstraněn (2026-04-28). " +
            "Delete externí vazby je nyní běžná operace, SQL CASCADE čistí bindings automaticky.");
        methodSlice.Should().NotContain("harvestLockedIds",
            "Symbol 'harvestLockedIds' byl odstraněn společně s pre-flight checkem.");
    }
}
