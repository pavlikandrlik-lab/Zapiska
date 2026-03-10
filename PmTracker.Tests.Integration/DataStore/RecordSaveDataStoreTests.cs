using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
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

    [Fact]
    public async Task SaveRecord_ShouldPersistFiveHundredCharacterGoal_WithPreservedLineBreak()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_goal_500_with_break");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordGoal500Admin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordGoal500Owner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RGOAL500");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RGOAL500_SUB", ownerId);
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
        var firstLine = new string('A', 249);
        var secondLine = new string('B', 250);
        var goalWithPadding = $"  {firstLine}\n{secondLine}  ";
        var normalizedGoal = $"{firstLine}\n{secondLine}";
        normalizedGoal.Length.Should().Be(500);

        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var recordId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = "Record with long goal",
            Cil = goalWithPadding,
            Popis = "Goal verification",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 3, 10),
            TerminUkonceni = new DateTime(2026, 3, 25),
            Subsystem = subsystemCode
        }, currentUser);

        var saved = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        saved.Cil.Should().Be(normalizedGoal);
        saved.Cil!.Length.Should().Be(500);
    }

    [Fact]
    public async Task BuildZaznamCreate_ShouldBootstrapPersistedScheduleSchema_WhenCatalogIsEmpty()
    {
        var db = await _fixture.CreateDatabaseAsync("record_create_bootstrap_schema");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        dbContext.CiselnikHarmonogramTypu.RemoveRange(await dbContext.CiselnikHarmonogramTypu.ToListAsync());
        dbContext.HarmonogramSablony.RemoveRange(await dbContext.HarmonogramSablony.ToListAsync());
        await dbContext.SaveChangesAsync();

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordCreateSchemaOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCSCHEMA");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCSCHEMA_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var model = store.BuildZaznamCreate(projectId);

        model.HarmonogramKroky.Should().NotBeEmpty();
        model.HarmonogramKroky.All(x => x.TrvaniTypId > 0).Should().BeTrue();

        (await dbContext.HarmonogramSablony.AsNoTracking().AnyAsync(x => x.IsAktivni)).Should().BeTrue();
        (await dbContext.CiselnikHarmonogramTypu.AsNoTracking().AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task SaveRecord_ShouldClearScheduleValues_WhenCategoryIsNotTask()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_clear_non_task_schedule");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordClearScheduleAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordClearScheduleOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCLEARSCH");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCLEARSCH_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        const string nonTaskCategoryCode = "INFO";
        var nonTaskCategoryExists = await dbContext.CiselnikKategoriiZaznamu
            .AnyAsync(x => x.Kod == nonTaskCategoryCode);
        if (!nonTaskCategoryExists)
        {
            var nonTaskCategory = new CiselnikKategoriiZaznamuEntity
            {
                Kod = nonTaskCategoryCode,
                Nazev = "Informace",
                IsLocked = false
            };
            dbContext.CiselnikKategoriiZaznamu.Add(nonTaskCategory);
            await dbContext.SaveChangesAsync();
        }

        var taskCategoryCode = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == "U" || x.Kod == "UKOL")
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

        var createModel = store.BuildZaznamCreate(projectId);
        var durationTypeId = createModel.HarmonogramKroky
            .Select(x => x.TrvaniTypId)
            .First(x => x > 0);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var recordId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = taskCategoryCode,
            Stav = statusCode,
            Nazev = "Task with temporary schedule",
            Popis = "Test",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 3, 6),
            TerminUkonceni = new DateTime(2026, 3, 20),
            Subsystem = subsystemCode,
            HarmonogramHodnoty = new List<SaveRecordHarmonogramValueCommand>
            {
                new()
                {
                    TypId = durationTypeId,
                    Hodnota = 6
                }
            }
        }, currentUser);

        (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .AnyAsync(x => x.ZaznamId == recordId)).Should().BeTrue();

        store.SaveRecord(new SaveRecordCommand
        {
            Id = recordId,
            ProjektId = projectId,
            Kategorie = nonTaskCategoryCode,
            Stav = statusCode,
            Nazev = "Converted to info",
            Popis = "No schedule expected",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 3, 6),
            TerminUkonceni = new DateTime(2026, 3, 20),
            Subsystem = subsystemCode,
            HarmonogramHodnoty = new List<SaveRecordHarmonogramValueCommand>
            {
                new()
                {
                    TypId = durationTypeId,
                    Hodnota = 9
                }
            }
        }, currentUser);

        (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
    }

    [Fact]
    public async Task SaveRecord_ShouldSanitizeRichDescriptionHtml()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_rich_description");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordRichDescAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordRichDescOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RRICHDSC");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RRICHDSC_SUB", ownerId);
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
            Nazev = "Record with rich description",
            Cil = "Goal",
            Popis = "<p><strong>Safe</strong><script>alert(1)</script><a href=\"javascript:alert(1)\">bad</a><a href=\"https://example.com\">ok</a></p>",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 3, 10),
            TerminUkonceni = new DateTime(2026, 3, 25),
            Subsystem = subsystemCode
        }, currentUser);

        var savedDescription = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => x.Popis)
            .SingleAsync();

        savedDescription.Should().NotBeNullOrWhiteSpace();
        savedDescription.Should().Contain("<strong>Safe</strong>");
        savedDescription.Should().Contain("https://example.com");
        savedDescription.Should().NotContain("<script");
        savedDescription.Should().NotContain("javascript:");
    }
}
