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
    public async Task BuildZaznamCreate_ShouldPreviewMeetingBasedNumber_AndIncrementOrder_WhenMeetingNumberingIsEnabled()
    {
        var db = await _fixture.CreateDatabaseAsync("record_create_number_preview_meeting");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordNumberPreviewAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordNumberPreviewOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RNUMPREV");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RNUMPREV_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
        project.PouzivatIdentJednani = true;
        await dbContext.SaveChangesAsync();

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", 920);

        // 1) Žádný záznam pro jednání zatím neexistuje → náhled ukazuje první volné pořadí "920-1"
        //    (dřív se chybně ukazovalo interní inkrementální CisloZaznamu).
        var firstModel = store.BuildZaznamCreate(projectId, meetingId);
        firstModel.CisloViditelne.Should().Be("920-1");
        var firstOption = firstModel.JednaniProCisloOptions.Single(x => x.Id == meetingId);
        firstOption.CisloJednani.Should().Be(920);
        firstOption.NextPoradiProCislo.Should().Be(1);

        // 2) Po založení záznamu do jednání se náhled posune na "920-2".
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.Where(x => x.Id == subsystemId).Select(x => x.Kod).SingleAsync();
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var savedRecordId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = "První záznam k jednání",
            Subsystem = subsystemCode,
            VlastnikId = ownerId,
            DatumZalozeni = new DateTime(2026, 5, 14),
            TerminUkonceni = new DateTime(2026, 5, 20),
            JednaniIdProCislo = meetingId
        }, currentUser);

        var savedRecord = await dbContext.ProjektoveZaznamy.AsNoTracking().FirstAsync(x => x.Id == savedRecordId);
        savedRecord.CisloViditelne.Should().Be("920-1");

        var secondModel = store.BuildZaznamCreate(projectId, meetingId);
        secondModel.CisloViditelne.Should().Be("920-2");
        secondModel.JednaniProCisloOptions.Single(x => x.Id == meetingId).NextPoradiProCislo.Should().Be(2);
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
            HarmonogramRezim = "Manual",
            HarmonogramHodnoty = new List<SaveRecordHarmonogramValueCommand>
            {
                new()
                {
                    Poradi = 2,
                    PlanDatum = new DateTime(2026, 3, 12),
                    SkutecnostDatum = new DateTime(2026, 3, 14)
                }
            }
        }, currentUser);

        (await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
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
            HarmonogramHodnoty = []
        }, currentUser);

        (await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
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
                    Poradi = 0
                },
                new()
                {
                    Poradi = 11
                }
            }
        };

        var action = () => store.SaveRecord(command, currentUser);
        var exception = action.Should().Throw<RecordValidationException>().Which;

        exception.FieldErrors.Keys.Should().Contain("Popis");
        exception.FieldErrors.Keys.Should().Contain("ExterniVazby[0].Cislo");
        exception.FieldErrors.Keys.Should().Contain("ExterniVazby[0].PredpokladanaCena");
        exception.FieldErrors.Keys.Should().Contain("VybraniSpolupracovniciIds");
        exception.FieldErrors.Keys.Should().Contain("HarmonogramHodnoty[0].Poradi");
        exception.FieldErrors.Keys.Should().Contain("HarmonogramHodnoty[1].Poradi");

        var popisError = exception.FieldErrors["Popis"].Single();
        popisError.Should().Contain("U+0001");
        popisError.Should().Contain("pozici");
        exception.DiagnosticLog.Should().Contain("CommandValues");
        exception.DiagnosticLog.Should().Contain("Record save validation failed.");
        exception.DiagnosticLog.Should().Contain("Popis");
    }

    /// <summary>
    /// Text požadavku se ukládá k vazbě a přežije opětovné uložení záznamu. UPSERT drží
    /// Id, takže FK z vyjadreni_vazby zůstanou platné (memory feedback_replace_upsert_for_audit_fk).
    ///
    /// Vazba se seeduje přímo do DB: v integrační fixture je ServiceDesk vypnutý a validace
    /// by NOVOU vazbu odmítla jako nenalezený tiket (stejný postup jako
    /// ExternalLinkDeleteWithBindingTests). Ověřuje se tedy UPDATE větev UPSERTu; že text
    /// ukládá i větev pro novou vazbu, hlídá zdrojový pin
    /// RecordServiceExternalLinkUpsertTests.ReplaceRecordExternalLinksAsync_UkladaPozadavekVObouVetvich.
    /// </summary>
    [Fact]
    public async Task SaveRecord_UlozitAZachovatTextPozadavkuUPnfVazby()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_pozadavek_pnf");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordPozadavekAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordPozadavekOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RPOZADAVEK");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RPOZADAVEK_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "Zaznam s pozadavkem");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).FirstAsync();

        // Vazba vzniká přímo v DB — bez zapnutého ServiceDesku by novou validace odmítla.
        var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var vazba = new ZaznamExterniOdkazEntity
        {
            ZaznamId = recordId,
            TypOdkazuId = pnfTypeId,
            Cislo = "336865"
        };
        dbContext.ZaznamExterniOdkazy.Add(vazba);
        await dbContext.SaveChangesAsync();
        var vazbaId = vazba.Id;
        vazba.Pozadavek.Should().BeNull("stávající vazby začínají prázdné");
        dbContext.ChangeTracker.Clear();

        SaveRecordCommand Prikaz(string? pozadavek) => new()
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
            ExterniVazby = new List<SaveRecordExterniVazbaCommand>
            {
                new()
                {
                    Id = vazbaId,
                    Typ = "PNF",
                    Cislo = "336865",
                    Pozadavek = pozadavek
                }
            }
        };

        store.SaveRecord(Prikaz("<p>Chceme sestavu.</p>"), currentUser);

        var poUlozeni = await dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == recordId && x.Cislo == "336865");
        poUlozeni.Id.Should().Be(vazbaId, "UPSERT nesmí vazbu smazat a založit znovu");
        poUlozeni.Pozadavek.Should().Contain("Chceme sestavu.");

        // Druhé uložení: text se přepíše, Id zůstává, takže FK z audit tabulek drží.
        store.SaveRecord(Prikaz("<p>Nove zadani.</p>"), currentUser);

        var poZmene = await dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == recordId && x.Cislo == "336865");
        poZmene.Id.Should().Be(vazbaId);
        poZmene.Pozadavek.Should().Contain("Nove zadani.");
        poZmene.Pozadavek.Should().NotContain("Chceme sestavu.");
    }

    /// <summary>
    /// Regrese z 2026-04-20: uložení záznamu přepisovalo VyzvaId podle textového pole Vyzva,
    /// které formulář od přechodu na přepínač neposílá. PNF po každém uložení vypadlo z výzvy
    /// do bufferu — i z odeslané, zamčené výzvy (spec 2026-09-10 §0).
    ///
    /// Scénář z hlášení uživatele: u PNF ve výzvě se změní jen předpokládaná cena.
    /// </summary>
    [Theory]
    [InlineData(VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano)]
    public async Task SaveRecord_ZmenaCenyPnfVeVyzve_ZachovaZarazeni(VyzvaStav stav)
    {
        var db = await _fixture.CreateDatabaseAsync($"record_save_keeps_vyzva_{(int)stav}");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordKeepVyzvaAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordKeepVyzvaOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RKEEPVYZVA");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RKEEPVYZVA_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "Zaznam s PNF ve vyzve");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).FirstAsync();

        var vyzva = new VyzvaEntity
        {
            ProjektId = projectId,
            Kod = "3/2026",
            PoradoveVRoce = 3,
            Rok = 2026,
            Stav = stav,
            DatumZalozeni = new DateTime(2026, 9, 1),
            ZalozilOsobaId = ownerId,
            DatumOdeslani = stav == VyzvaStav.Odeslano ? new DateTime(2026, 9, 5) : null,
            OdeslalOsobaId = stav == VyzvaStav.Odeslano ? ownerId : null,
            MistoPlneniSnapshot = "FIS (EIS): VZ 8201",
            CisloRamcoveSmlouvySnapshot = "INT-SML-KEEP",
        };
        dbContext.Vyzvy.Add(vyzva);
        await dbContext.SaveChangesAsync();

        // Vazba vzniká přímo v DB — novou vazbu by validace bez ServiceDesku odmítla.
        var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var vazba = new ZaznamExterniOdkazEntity
        {
            ZaznamId = recordId,
            TypOdkazuId = pnfTypeId,
            Cislo = "336865",
            PredpokladanaCena = 1000m,
            ZaradidDoVyzvy = true,
            VyzvaId = vyzva.Id,
        };
        dbContext.ZaznamExterniOdkazy.Add(vazba);
        await dbContext.SaveChangesAsync();
        var vazbaId = vazba.Id;
        dbContext.ChangeTracker.Clear();

        // Přesně to, co posílá formulář: Id, typ, číslo, cenu a skryté VyzvaId + ZaradidDoVyzvy.
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
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu,
            ExterniVazby = new List<SaveRecordExterniVazbaCommand>
            {
                new()
                {
                    Id = vazbaId,
                    Typ = "PNF",
                    Cislo = "336865",
                    PredpokladanaCena = "2000",
                    VyzvaId = vyzva.Id,
                    ZaradidDoVyzvy = true,
                },
            },
        }, currentUser);

        var poUlozeni = await dbContext.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == vazbaId);
        poUlozeni.PredpokladanaCena.Should().Be(2000m, "změna ceny se uložit musí");
        poUlozeni.VyzvaId.Should().Be(vyzva.Id, "uložení záznamu nesmí PNF vytáhnout z výzvy");
        poUlozeni.ZaradidDoVyzvy.Should().BeTrue("přepínač zůstává zapnutý");
    }
}
