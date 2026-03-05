using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordSaveDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordSaveDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveRecord_ShouldBootstrapDefaultScheduleSchema_WhenCatalogIsEmpty()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_bootstrap_schema");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        dbContext.CiselnikHarmonogramTypu.RemoveRange(await dbContext.CiselnikHarmonogramTypu.ToListAsync());
        dbContext.HarmonogramSablony.RemoveRange(await dbContext.HarmonogramSablony.ToListAsync());
        await dbContext.SaveChangesAsync();

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordSchemaAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordSchemaOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RSCHEMA");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RSCHEMA_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var categoryCode = await dbContext.CiselnikKategoriiZaznamu
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var subsystemCode = await dbContext.Subsystemy
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .SingleAsync();

        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var recordId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = "Schema bootstrap record",
            Cil = "  Schema bootstrap goal  ",
            Popis = "Test",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 3, 4),
            TerminUkonceni = new DateTime(2026, 3, 18),
            Subsystem = subsystemCode
        }, currentUser);

        recordId.Should().BeGreaterThan(0);
        (await dbContext.HarmonogramSablony.AnyAsync(x => x.IsAktivni)).Should().BeTrue();
        (await dbContext.CiselnikHarmonogramTypu.AnyAsync()).Should().BeTrue();

        var saved = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        saved.HarmonogramSablonaVerze.Should().BeGreaterThan(0);
        saved.Cil.Should().Be("Schema bootstrap goal");
        (await dbContext.HarmonogramSablony.AsNoTracking()
            .AnyAsync(x => x.Verze == saved.HarmonogramSablonaVerze)).Should().BeTrue();
    }
}
