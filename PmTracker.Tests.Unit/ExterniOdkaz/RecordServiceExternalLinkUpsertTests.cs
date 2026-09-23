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
        // 2026-04-28: pre-flight harvest_locked check byl ODSTRANĚN. Delete externí vazby
        // je běžná operace, aplikace cleanup-uje vyjadreni_vazby explicitně (viz následující test).
        var source = LoadServiceSource();
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        methodSlice.Should().NotContain("external_link_harvest_locked",
            "Pre-flight check 'external_link_harvest_locked' byl odstraněn — delete je běžná operace.");
        methodSlice.Should().NotContain("harvestLockedIds",
            "Symbol 'harvestLockedIds' byl odstraněn společně s pre-flight checkem.");
    }

    [Fact]
    public void ReplaceRecordExternalLinksAsync_MustCleanupVyjadreniVazbyBeforeExternalLinkDelete()
    {
        // 2026-04-28: FK_zhvv_externi_odkaz zůstává NO ACTION (multi-cascade-path constraint
        // SQL 1785 — vyjadreni_vazby má dva FK na projektove_zaznamy). Aplikace musí
        // explicitně cleanup-ovat navázané vyjadreni_vazby rows PŘED RemoveRange externí vazby.
        // POZOR: tenhle test čte zdrojový kód jako text — o pořadí SQL příkazů neříká nic.
        // To zajišťuje deklarovaný vztah v ZaznamHarmonogramVyjadreniVazbaEntityConfiguration
        // a hlídá ho ExternalLinkDeleteWithBindingTests proti reálnému SQL Serveru.
        var source = LoadServiceSource();
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        methodSlice.Should().Contain("VyjadreniVazby",
            "Metoda musí dotázat VyjadreniVazby DbSet pro identifikaci bindings k cleanup.");
        methodSlice.Should().Contain("bindingsToCleanup",
            "Lokal 'bindingsToCleanup' identifikuje pattern explicit pre-cleanup před delete externí vazby.");
        methodSlice.Should().Contain("VyjadreniVazby.RemoveRange(bindingsToCleanup)",
            "Aplikace musí explicitně RemoveRange vyjadreni_vazby PŘED RemoveRange externí vazby.");

        // Pořadí: VyjadreniVazby.RemoveRange MUSÍ předcházet ZaznamExterniOdkazy.RemoveRange(toDelete)
        var indexOfBindingsCleanup = methodSlice.IndexOf("VyjadreniVazby.RemoveRange(bindingsToCleanup)", StringComparison.Ordinal);
        var indexOfExterniOdkazyDelete = methodSlice.IndexOf("ZaznamExterniOdkazy.RemoveRange(toDelete)", StringComparison.Ordinal);
        indexOfBindingsCleanup.Should().BeGreaterThan(0, "VyjadreniVazby cleanup musí v kódu existovat.");
        indexOfExterniOdkazyDelete.Should().BeGreaterThan(indexOfBindingsCleanup,
            "VyjadreniVazby.RemoveRange(bindingsToCleanup) musí být PŘED ZaznamExterniOdkazy.RemoveRange(toDelete).");
    }

    /// <summary>
    /// Text požadavku je pole formuláře, takže ho UPSERT musí propsat v OBOU větvích —
    /// při úpravě existující vazby i při založení nové. Vynechání jedné z nich by se
    /// projevilo jako „text se občas neuloží" (spec 2026-09-08 §5.5).
    /// </summary>
    [Fact]
    public void ReplaceRecordExternalLinksAsync_UkladaPozadavekVObouVetvich()
    {
        var source = LoadServiceSource();
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        methodIndex.Should().BeGreaterThan(0);

        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        methodSlice.Should().Contain("existingEntity.Pozadavek = pozadavek",
            "úprava existující vazby musí text přepsat");
        methodSlice.Should().Contain("Pozadavek = pozadavek,",
            "nová vazba musí text uložit rovnou při založení");
        methodSlice.Should().Contain("HasVisibleText",
            "prázdný odstavec z Quillu (<p><br></p>) se nesmí uložit jako text");
    }

    /// <summary>
    /// Zařazení do výzvy vlastní VyzvaService (spec 2026-09-10 R0.1). UPSERT u existující vazby
    /// nesmí přepsat VyzvaId ani ZaradidDoVyzvy — dřív to dělal podle pole, které formulář
    /// neposílá, a každé uložení vytáhlo PNF z výzvy.
    /// </summary>
    [Fact]
    public void ReplaceRecordExternalLinksAsync_NesahaNaZarazeniExistujiciVazby()
    {
        var source = LoadServiceSource();
        var start = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        var metoda = end < 0 ? source[start..] : source[start..end];

        metoda.Should().NotContain("existingEntity.VyzvaId", "zařazení do výzvy vlastní VyzvaService");
        metoda.Should().NotContain("existingEntity.ZaradidDoVyzvy", "přepínač se ukládá vlastním endpointem");
        source.Should().NotContain("ResolveVyzvaIdAsync", "mrtvé pole Vyzva se už nikam nepřekládá");
    }

    /// <summary>
    /// Nová vazba nemá Id, takže přepínač nemohl zavolat set-zaradid — stav nese formulář.
    /// Do bufferu smí jen PNF a jen s oprávněním, které hlídá i endpoint přepínače (R0.2).
    /// Integration sada novou vazbu přes uložení nezaloží (ServiceDesk je vypnutý), proto pin.
    /// </summary>
    [Fact]
    public void ReplaceRecordExternalLinksAsync_NovaPnfJdeDoBufferuJenSOpravnenim()
    {
        var source = LoadServiceSource();

        source.Should().Contain("ZaradidDoVyzvy = link.ZaradidDoVyzvy && smiZaraditDoBufferu && JePnf(link.Typ)",
            "nová vazba bere stav přepínače z formuláře, ale jen PNF a jen s oprávněním");
        source.Should().Contain("currentUser.HasPermission(PermissionKeys.VyzvyPnfAssign, command.ProjektId)",
            "stejné oprávnění, jaké hlídá endpoint přepínače");
    }

    /// <summary>Snímek skutečné ceny vlastní harvest — uložení záznamu na něj nesahá (R8).</summary>
    [Fact]
    public void RecordService_NesahaNaSnimekKalkulace()
    {
        LoadServiceSource().Should().NotContain("Kalkulace",
            "KalkulaceCena/Id/Nacteno plní jen IKalkulaceSnapshotService");
    }
}
