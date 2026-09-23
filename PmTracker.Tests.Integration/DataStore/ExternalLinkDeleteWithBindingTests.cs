using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Reprodukce hlášení 2026-09-06: smazání externí vazby, na kterou ukazují harvestované
/// bindingy, padá na FK_zhvv_externi_odkaz (SQL 547). Stávající pokrytí je jen textové
/// (RecordServiceExternalLinkUpsertTests čte zdroják), takže se skutečné pořadí DELETE
/// příkazů proti reálnému FK nikdy neprošlo.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ExternalLinkDeleteWithBindingTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ExternalLinkDeleteWithBindingTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveRecord_ShouldDeleteExternalLink_WhenHarvestBindingReferencesIt()
    {
        var db = await _fixture.CreateDatabaseAsync("external_link_delete_binding");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LinkDeleteAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LinkDeleteOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "LINKDEL1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "LINKDEL1_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "Zaznam s PNF");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).FirstAsync();

        SaveRecordCommand BuildCommand(IReadOnlyList<SaveRecordExterniVazbaCommand> links) => new()
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu,
            ExterniVazby = links.ToList()
        };

        // 1) Externí vazba PNF — zakládá se přímo do DB, protože v integrační fixture
        // je ServiceDesk vypnutý a validace by nový tiket odmítla jako nenalezený.
        var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var link = new ZaznamExterniOdkazEntity
        {
            ZaznamId = recordId,
            TypOdkazuId = pnfTypeId,
            Cislo = "123456"
        };
        dbContext.ZaznamExterniOdkazy.Add(link);
        await dbContext.SaveChangesAsync();
        var linkId = link.Id;

        // 2) Harvest na ni navěsí binding (v provozu to dělá VyjadreniHarvestService).
        dbContext.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = recordId,
            Poradi = 4,
            ExterniOdkazId = linkId,
            HotVyjadreniId = 987654,
            DatumVyjadreni = new DateTime(2026, 9, 1),
            Source = 0,
            Stav = 0,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // 3) Uživatel vazbu ze záznamu odebere.
        var deleting = () => store.SaveRecord(BuildCommand([]), currentUser);

        deleting.Should().NotThrow(
            "odebrání externí vazby musí uklidit navázané bindingy dřív, než smaže samotnou vazbu");

        var remainingLinks = await dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .CountAsync(x => x.ZaznamId == recordId);
        remainingLinks.Should().Be(0, "vazba měla být smazána");

        var remainingBindings = await dbContext.VyjadreniVazby.AsNoTracking()
            .CountAsync(x => x.ExterniOdkazId == linkId);
        remainingBindings.Should().Be(0, "navázané bindingy měly zmizet s ní");
    }
}
