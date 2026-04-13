using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

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
    public async Task SaveRecord_ShouldPersistUpdatedCreatedDate_ForExistingRecord()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_update_created_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordEditCreatedDateAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordEditCreatedDateOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RDATEEDIT");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RDATEEDIT_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Editable created date");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => new
            {
                x.Id,
                x.ProjektId,
                x.KategorieId,
                x.StavUkoluId,
                x.CisloZaznamu,
                x.Nazev,
                x.Cil,
                x.Popis,
                x.VlastnikId,
                x.DatumUkonceni,
                x.SubsystemId
            })
            .FirstAsync();
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId)
            .Select(x => x.Kod)
            .FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId)
            .Select(x => x.Kod)
            .FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId)
            .Select(x => x.Kod)
            .FirstAsync();
        var newCreatedDate = record.DatumUkonceni.Date.AddDays(-1);

        store.SaveRecord(new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = newCreatedDate,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu
        }, currentUser);

        var saved = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        saved.DatumZalozeni.Date.Should().Be(newCreatedDate.Date);
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

        model.HarmonogramBlok.Kroky.Should().NotBeEmpty();
        model.HarmonogramBlok.Kroky.All(x => x.TrvaniTypId > 0).Should().BeTrue();

        (await dbContext.HarmonogramSablony.AsNoTracking().AnyAsync(x => x.IsAktivni)).Should().BeTrue();
        (await dbContext.CiselnikHarmonogramTypu.AsNoTracking().AnyAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task BuildZaznamCreate_ShouldPrefillMeetingAndStartDate_WhenContextMeetingIsOpen_AndMeetingNumberingIsEnabled()
    {
        var db = await _fixture.CreateDatabaseAsync("record_create_context_meeting_open");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordContextMeetingOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCMEETOPEN");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCMEETOPEN_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
        project.PouzivatIdentJednani = true;
        await dbContext.SaveChangesAsync();

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", 920);
        var meetingDate = new DateTime(2026, 5, 14);
        var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = meetingDate;
        await dbContext.SaveChangesAsync();

        var model = store.BuildZaznamCreate(projectId, meetingId);

        model.PouzivatIdentJednani.Should().BeTrue();
        model.JednaniIdProCislo.Should().Be(meetingId);
        model.DatumZalozeni.Date.Should().Be(meetingDate.Date);
        model.JednaniProCisloOptions.Should().ContainSingle(x => x.Id == meetingId && x.Datum.Date == meetingDate.Date);
    }

    [Fact]
    public async Task BuildZaznamCreate_ShouldFallbackToOpenMeeting_WhenContextMeetingIsClosed_AndMeetingNumberingIsEnabled()
    {
        var db = await _fixture.CreateDatabaseAsync("record_create_context_meeting_closed");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordContextClosedOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCMEETCLS");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCMEETCLS_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
        project.PouzivatIdentJednani = true;
        await dbContext.SaveChangesAsync();

        var openMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", 931);
        var openMeetingDate = new DateTime(2026, 6, 10);
        var openMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == openMeetingId);
        openMeeting.DatumPlanovane = openMeetingDate;

        var closedMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "CLOSED", 940);
        var closedMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == closedMeetingId);
        closedMeeting.DatumPlanovane = new DateTime(2026, 6, 25);
        await dbContext.SaveChangesAsync();

        var model = store.BuildZaznamCreate(projectId, closedMeetingId);

        model.JednaniIdProCislo.Should().Be(openMeetingId);
        model.DatumZalozeni.Date.Should().Be(openMeetingDate.Date);
    }

    [Fact]
    public async Task BuildZaznamCreate_ShouldUseContextMeetingDate_WhenProjectDoesNotUseMeetingNumbering()
    {
        var db = await _fixture.CreateDatabaseAsync("record_create_context_meeting_plain");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordContextPlainOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCMEETPLN");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCMEETPLN_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var contextMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", 951);
        var contextMeetingDate = new DateTime(2026, 7, 3);
        var contextMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == contextMeetingId);
        contextMeeting.DatumPlanovane = contextMeetingDate;
        await dbContext.SaveChangesAsync();

        var model = store.BuildZaznamCreate(projectId, contextMeetingId);

        model.PouzivatIdentJednani.Should().BeFalse();
        model.JednaniIdProCislo.Should().BeNull();
        model.DatumZalozeni.Date.Should().Be(contextMeetingDate.Date);
    }

    [Fact]
    public async Task BuildZaznamCreate_ShouldKeepContextMeetingDate_WhenNoOpenMeetingExists_ForMeetingNumbering()
    {
        var db = await _fixture.CreateDatabaseAsync("record_create_context_meeting_no_open");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordContextNoOpenOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCMEETNOP");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCMEETNOP_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
        project.PouzivatIdentJednani = true;
        await dbContext.SaveChangesAsync();

        var contextMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "CLOSED", 961);
        var contextMeetingDate = new DateTime(2026, 7, 24);
        var contextMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == contextMeetingId);
        contextMeeting.DatumPlanovane = contextMeetingDate;
        await dbContext.SaveChangesAsync();

        var model = store.BuildZaznamCreate(projectId, contextMeetingId);

        model.MaDostupneJednaniProCislo.Should().BeFalse();
        model.JednaniIdProCislo.Should().BeNull();
        model.DatumZalozeni.Date.Should().Be(contextMeetingDate.Date);
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
        var durationTypeId = createModel.HarmonogramBlok.Kroky
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

    [Fact]
    public async Task SaveRecord_ShouldAggregateCrossTabValidationIssues_WithDiagnosticLog()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_cross_tab_validation");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordCrossTabAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordCrossTabOwner");
        var outsiderId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordCrossTabOutsider");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCROSSTAB");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCROSSTAB_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var categoryCode = await dbContext.CiselnikKategoriiZaznamu
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
        var durationTypeId = createModel.HarmonogramBlok.Kroky
            .Select(x => x.TrvaniTypId)
            .First(x => x > 0);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var command = new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = "Record validation",
            Cil = "valid",
            Popis = $"Text s nepovolenym znakem {char.ConvertFromUtf32(1)} uvnitr",
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 4, 1),
            TerminUkonceni = new DateTime(2026, 4, 20),
            Subsystem = subsystemCode,
            VybraniSpolupracovniciIds = new List<int> { outsiderId },
            ExterniVazby = new List<SaveRecordExterniVazbaCommand>
            {
                new()
                {
                    Typ = "PMP",
                    Cislo = "",
                    PredpokladanaCena = "neni-cislo"
                }
            },
            HarmonogramHodnoty = new List<SaveRecordHarmonogramValueCommand>
            {
                new()
                {
                    TypId = durationTypeId,
                    Hodnota = -4
                },
                new()
                {
                    TypId = 999999,
                    Hodnota = 5
                }
            }
        };

        var action = () => store.SaveRecord(command, currentUser);
        var exception = action.Should().Throw<RecordValidationException>().Which;

        exception.FieldErrors.Keys.Should().Contain("Popis");
        exception.FieldErrors.Keys.Should().Contain("ExterniVazby[0].Cislo");
        exception.FieldErrors.Keys.Should().Contain("ExterniVazby[0].PredpokladanaCena");
        exception.FieldErrors.Keys.Should().Contain("VybraniSpolupracovniciIds");
        exception.FieldErrors.Keys.Should().Contain("HarmonogramHodnoty[0].Hodnota");
        exception.FieldErrors.Keys.Should().Contain("HarmonogramHodnoty[1].TypId");

        var popisError = exception.FieldErrors["Popis"].Single();
        popisError.Should().Contain("U+0001");
        popisError.Should().Contain("pozici");
        exception.DiagnosticLog.Should().Contain("CommandValues");
        exception.DiagnosticLog.Should().Contain("Record save validation failed.");
        exception.DiagnosticLog.Should().Contain("Popis");
    }
}
