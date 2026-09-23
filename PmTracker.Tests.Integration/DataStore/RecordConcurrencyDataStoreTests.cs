using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-09-17 §6.1 — zásah automatu do harmonogramu nesmí blokovat uložení
/// záznamu. Přesně tohle v pilotu padalo hláškou „Harmonogram byl mezitím upraven
/// jiným uživatelem", i když šlo o automat a uživatel měnil jen název.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordConcurrencyDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordConcurrencyDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveRecord_ShouldSucceed_WhenAutomatTouchedScheduleAfterEditorLoaded()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_after_automat_touch");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "AUTOTOUCH");

        // Automat sáhne do skutečnosti kroku 4 poté, co si uživatel otevřel editor.
        var krok = await dbContext.ZaznamHarmonogramKroky
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum = new DateTime(2026, 9, 1);
        krok.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;
        krok.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Uživatel mění jen název — save musí projít.
        var act = () => store.SaveRecord(scenario.BuildRenameCommand("Novy nazev"), scenario.CurrentUser);

        act.Should().NotThrow<RecordValidationException>();
        (await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == scenario.RecordId))
            .Nazev.Should().Be("Novy nazev");
    }

    /// <summary>
    /// Spec §5.1 — invariant, na kterém celý návrh stojí: zásah automatu do kroků
    /// nesmí posunout verzi záznamu, jinak se falešné konflikty vrátí zadními vrátky.
    /// </summary>
    [Fact]
    public async Task AutomatScheduleWrite_ShouldNotChangeRecordVersion()
    {
        var db = await _fixture.CreateDatabaseAsync("record_version_automat_stable");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "VERSTABLE");

        var versionBefore = await RecordVersionQuery.ResolveVersionTokenAsync(
            dbContext, scenario.RecordId, CancellationToken.None);

        var krok = await dbContext.ZaznamHarmonogramKroky
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum = new DateTime(2026, 9, 1);
        krok.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;
        krok.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var versionAfter = await RecordVersionQuery.ResolveVersionTokenAsync(
            dbContext, scenario.RecordId, CancellationToken.None);

        versionAfter.Should().Be(versionBefore,
            "automat neaudituje, takže verze záznamu musí zůstat stejná");
    }

    /// <summary>Spec §5.2 — cizí lidský zápis se pozná a uložení se odmítne.</summary>
    [Fact]
    public async Task SaveRecord_ShouldThrowRecordStale_WhenAnotherUserSavedMeanwhile()
    {
        var db = await _fixture.CreateDatabaseAsync("record_version_stale_reject");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "STALE");

        // Verze, se kterou si první uživatel vyrenderoval editor.
        var versionInEditor = await RecordVersionQuery.ResolveVersionTokenAsync(
            dbContext, scenario.RecordId, CancellationToken.None);

        // Druhý uživatel mezitím uloží.
        store.SaveRecord(scenario.BuildRenameCommand("Ulozil druhy uzivatel"), scenario.CurrentUser);

        // První uživatel odesílá formulář se starou verzí.
        var staleCommand = scenario.BuildRenameCommand("Ulozil prvni uzivatel");
        staleCommand.RecordVersion = versionInEditor;
        var act = () => store.SaveRecord(staleCommand, scenario.CurrentUser);

        act.Should().Throw<RecordStaleException>()
            .WithMessage("*jiný uživatel*");
        (await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == scenario.RecordId))
            .Nazev.Should().Be("Ulozil druhy uzivatel", "cizí zápis se nesmí přepsat");
    }

    /// <summary>Spec §5.2 — čerstvá verze projde.</summary>
    [Fact]
    public async Task SaveRecord_ShouldSucceed_WhenVersionIsCurrent()
    {
        var db = await _fixture.CreateDatabaseAsync("record_version_current_ok");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "VERFRESH");

        var command = scenario.BuildRenameCommand("Aktualni verze projde");
        command.RecordVersion = await RecordVersionQuery.ResolveVersionTokenAsync(
            dbContext, scenario.RecordId, CancellationToken.None);

        var act = () => store.SaveRecord(command, scenario.CurrentUser);

        act.Should().NotThrow<RecordStaleException>();
    }

    /// <summary>
    /// Regrese po Fázi 1: nová kontrola verze se vložila do existujícího if/else-if řetězu
    /// a vyřadila kontrolu příslušnosti záznamu k projektu. Ta je přitom jediná vazba mezi
    /// Id záznamu z formuláře a ProjektId, proti kterému controller ověřuje oprávnění —
    /// bez ní by editační právo na jednom projektu otevřelo záznamy všech ostatních.
    /// </summary>
    [Fact]
    public async Task SaveRecord_ShouldRejectForeignProject_EvenWhenRecordVersionIsCurrent()
    {
        var db = await _fixture.CreateDatabaseAsync("record_cross_project_guard");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "XPROJ");
        var foreignProjectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "XPROJ2");

        var command = scenario.BuildRenameCommand("Pokus o cizi projekt");
        command.ProjektId = foreignProjectId;
        command.RecordVersion = await RecordVersionQuery.ResolveVersionTokenAsync(
            dbContext, scenario.RecordId, CancellationToken.None);

        var act = () => store.SaveRecord(command, scenario.CurrentUser);

        act.Should().Throw<RecordValidationException>()
            .Which.Issues.Should().Contain(
                issue => issue.Rule == "record_project_mismatch",
                "záznam z cizího projektu se nesmí uložit ani s platnou verzí");
    }

    /// <summary>
    /// Verzi posouvá KAŽDÝ auditní zápis nad záznamem, ne jen uložení editoru — například
    /// doplnění identifikátoru z jednání (action „assign“). Hláška proto musí pojmenovat
    /// autora právě toho posledního zápisu; dřívější dotaz filtroval na update/approve
    /// a v tomhle případě nepojmenoval nikoho.
    /// </summary>
    [Fact]
    public async Task StaleMessage_ShouldNameActorOfLatestAuditEntry_NotJustLastUpdate()
    {
        var db = await _fixture.CreateDatabaseAsync("record_stale_names_latest_actor");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LASTACT");

        var versionInEditor = await RecordVersionQuery.ResolveVersionTokenAsync(
            dbContext, scenario.RecordId, CancellationToken.None);

        var jinyOsobaId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "Zdenek");
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = jinyOsobaId,
            EntityType = "zaznam",
            EntityId = scenario.RecordId.ToString(),
            Action = "assign",
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var command = scenario.BuildRenameCommand("Pokus po cizim assignu");
        command.RecordVersion = versionInEditor;
        var act = () => store.SaveRecord(command, scenario.CurrentUser);

        act.Should().Throw<RecordStaleException>()
            .WithMessage("*Tester Zdenek*", "hláška musí pojmenovat autora posledního zápisu");
    }
}
