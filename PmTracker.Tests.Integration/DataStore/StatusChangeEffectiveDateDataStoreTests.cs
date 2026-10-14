using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Změna stavu platná k jednání (spec 2026-10-13): co uložení změny stavu zapíše do historie,
/// co nabídne editor a jak se historie chová při přeplánování a smazání jednání.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class StatusChangeEffectiveDateDataStoreTests
{
    // 13.10.2026 00:30 v Praze = 12.10. 22:30 UTC — místní a UTC datum se liší.
    private static readonly DateTimeOffset JustAfterMidnightInPrague = new(2026, 10, 12, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateTime RecordFoundedOn = new(2026, 9, 1);

    private readonly SqlIntegrationFixture _fixture;

    public StatusChangeEffectiveDateDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    /// <summary>Review focus 2: formulář bez pole StavPlatiK — změna platí k dnešnímu místnímu datu.</summary>
    [Fact]
    public async Task SaveRecord_StatusChangeWithoutChoice_IsEffectiveOnLocalToday_AndKeepsUtcMomentOfSaving()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_today");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(JustAfterMidnightInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFTODAY");

        store.SaveRecord(await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode), arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == arranged.RecordId);
        change.DatumZmeny.Should().Be(new DateTime(2026, 10, 12, 22, 30, 0), "datum_zmeny zůstává okamžik uložení v UTC");
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 13), "„dnes“ je místní datum, ne datum v UTC");
        change.JednaniId.Should().BeNull();
    }

    // 13.10.2026 09:00 v Praze.
    private static readonly DateTimeOffset MorningInPrague = new(2026, 10, 13, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveRecord_StatusChangeBoundToMeeting_StoresMeetingDateAndId()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_meeting");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFMEET");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));

        store.SaveRecord(
            await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode, stavPlatiK: meeting888.ToString()),
            arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.ZaznamId == arranged.RecordId);
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 8));
        change.JednaniId.Should().Be(meeting888);
        change.DatumZmeny.Should().Be(new DateTime(2026, 10, 13, 7, 0, 0), "okamžik uložení zůstává pro audit");
    }

    [Fact]
    public async Task SaveRecord_ClosedMeeting_IsRejected_AndNothingIsSaved()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_closed");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFCLOSED");
        var closed886 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "CLOSED", 886, new DateTime(2026, 9, 24));
        var statusBefore = await CurrentStatusAsync(dbContext, arranged.RecordId);

        var command = await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode, stavPlatiK: closed886.ToString());
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().StartWith("Jednání č. 886 teď nejde zvolit");
        (await CurrentStatusAsync(dbContext, arranged.RecordId)).Should().Be(statusBefore);
        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().AnyAsync(x => x.ZaznamId == arranged.RecordId)).Should().BeFalse();
    }

    /// <summary>Review focus 3: id jednání z jiného projektu se nesmí přijmout.</summary>
    [Fact]
    public async Task SaveRecord_MeetingFromAnotherProject_IsRejected()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_other_project");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFOTHER");
        var otherProjectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SEFOTHER2");
        var foreignMeeting = await CreateDatedMeetingAsync(dbContext, otherProjectId, "OPEN", 500, new DateTime(2026, 10, 8));

        var command = await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode, stavPlatiK: foreignMeeting.ToString());
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().Be("Vybrané jednání neexistuje nebo bylo smazáno. Vyberte znovu, k čemu změna stavu platí.");
    }

    [Fact]
    public async Task SaveRecord_EffectiveDateBeforePreviousChange_IsRejected()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_lower_bound");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFBOUND");
        var meeting887 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 887, new DateTime(2026, 10, 1));
        await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));
        // Předchozí změna stavu platí k 8.10.: úkol je teď hotový, uživatel ho znovu otevírá.
        await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 8), jednaniId: null);
        var runningCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();

        var command = await BuildEditCommandAsync(dbContext, arranged.RecordId, runningCode, stavPlatiK: meeting887.ToString());
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().Be("Změna stavu nemůže platit dříve než 08.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    /// <summary>Review focus 5: posunuté datum založení v témže uložení funguje jako spodní mez.</summary>
    [Fact]
    public async Task SaveRecord_FoundingDateMovedAfterChosenMeeting_IsRejected()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_founding");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFFOUND");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));

        var command = await BuildEditCommandAsync(
            dbContext, arranged.RecordId, arranged.DoneStatusCode,
            stavPlatiK: meeting888.ToString(), datumZalozeni: new DateTime(2026, 10, 10));
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().Be("Změna stavu nemůže platit dříve než 10.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    /// <summary>Review focus 1: nezměněný stav — pole StavPlatiK se ignoruje, i když je nesmyslné.</summary>
    [Fact]
    public async Task SaveRecord_UnchangedStatus_IgnoresStavPlatiK_AndWritesNoHistory()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_unchanged");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFSAME");
        var currentStatusId = await CurrentStatusAsync(dbContext, arranged.RecordId);
        var currentCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == currentStatusId)
            .Select(x => x.Kod)
            .SingleAsync();

        store.SaveRecord(await BuildEditCommandAsync(dbContext, arranged.RecordId, currentCode, stavPlatiK: "999999"), arranged.User);

        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().AnyAsync(x => x.ZaznamId == arranged.RecordId)).Should().BeFalse();
    }

    [Fact]
    public async Task Query_LockedMeeting_StopsTheOfferLikeClosedOne()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_locked");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var arranged = await ArrangeTaskAsync(dbContext, "SEFLOCK");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var locked888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));
        await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 887, new DateTime(2026, 10, 1));
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == locked888);
        meeting.UzamklOsobaId = arranged.User.OsobaId;
        await dbContext.SaveChangesAsync();

        var context = await StatusChangeEffectiveDateQuery.LoadAsync(
            dbContext, arranged.ProjectId, arranged.RecordId, RecordFoundedOn, new DateTime(2026, 10, 13), CancellationToken.None);

        context.Choice.Options.Select(x => x.JednaniId).Should().Equal(null, meeting889);
    }

    [Fact]
    public async Task BuildZaznamEdit_OffersChoice_ForExistingRecord()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_editor");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFEDIT");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));

        var editor = store.BuildZaznamEdit(arranged.RecordId);

        editor.StavPlatiK.Should().NotBeNull();
        editor.StavPlatiK!.Options.Select(x => x.JednaniId).Should().Equal(null, meeting888);
        editor.StavPlatiK.DefaultValue.Should().Be(meeting888.ToString());
        editor.StavPlatiK.Options[0].Label.Should().Be("Dnes (13.10.2026)");
    }

    [Fact]
    public async Task BuildZaznamCreate_HasNoChoice()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_create");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFCREATE");

        store.BuildZaznamCreate(arranged.ProjectId).StavPlatiK.Should().BeNull("založení nezapisuje řádek historie stavů (spec §4.1)");
    }

    [Fact]
    public async Task SaveMeeting_Reschedule_MovesBoundStatusChanges()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_reschedule");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFMOVE");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var changeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 22), jednaniId: meeting889);

        store.SaveMeeting(MeetingCommand(meeting889, arranged.ProjectId, 889, new DateTime(2026, 10, 20), "DRAFT"), arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == changeId);
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 20), "změna navázaná na jednání jde s jeho datem (spec §4.6)");
        change.JednaniId.Should().Be(meeting889);
    }

    [Fact]
    public async Task SaveMeeting_RescheduleBreakingOrderOfChanges_IsRejected_AndNothingChanges()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_reschedule_order");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFORDER");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        // Ukončeno k jednání 889, pak 25.10. znovu otevřeno „dnes“.
        var closeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 22), jednaniId: meeting889);
        await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: false, platiOd: new DateTime(2026, 10, 25), jednaniId: null);
        var visibleNumber = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == arranged.RecordId)
            .Select(x => x.CisloViditelne ?? x.CisloZaznamu.ToString())
            .SingleAsync();

        var act = () => store.SaveMeeting(
            MeetingCommand(meeting889, arranged.ProjectId, 889, new DateTime(2026, 10, 29), "DRAFT"), arranged.User);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Jednání nelze přesunout na 29.10.2026: změna stavu záznamů č. {visibleNumber.Trim()} navázaná na toto jednání*");
        (await dbContext.Jednani.AsNoTracking().SingleAsync(x => x.Id == meeting889)).DatumPlanovane.Should().Be(new DateTime(2026, 10, 22));
        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == closeId)).PlatiOd.Should().Be(new DateTime(2026, 10, 22));
    }

    /// <summary>
    /// Review focus 4: úprava jednání bez změny data na historii nesahá. Navázaná změna má
    /// záměrně jiné datum než jednání — kdyby přepis proběhl, test to pozná.
    /// </summary>
    [Fact]
    public async Task SaveMeeting_WithoutDateChange_DoesNotTouchStatusHistory()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_meeting_same_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFPLACE");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var changeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 21), jednaniId: meeting889);

        var command = MeetingCommand(meeting889, arranged.ProjectId, 889, new DateTime(2026, 10, 22), "DRAFT");
        command.Misto = "Jiná místnost";
        store.SaveMeeting(command, arranged.User);

        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == changeId))
            .PlatiOd.Should().Be(new DateTime(2026, 10, 21));
    }

    [Fact]
    public async Task DeleteMeeting_KeepsPlatiOd_AndClearsBinding()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_meeting_delete");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFDELETE");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));
        var changeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 8), jednaniId: meeting888);

        store.DeleteMeeting(new DeleteMeetingCommand { JednaniId = meeting888, ProjektId = arranged.ProjectId }, arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == changeId);
        change.JednaniId.Should().BeNull();
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 8), "změna dál platí k datu, které jednání mělo");
        (await dbContext.Jednani.AsNoTracking().AnyAsync(x => x.Id == meeting888)).Should().BeFalse();
    }

    private static SaveMeetingCommand MeetingCommand(int id, int projectId, int number, DateTime date, string stateCode) => new()
    {
        Id = id,
        ProjektId = projectId,
        CisloJednani = number,
        DatumPlanovane = date,
        CasZacatek = new TimeOnly(9, 0),
        Misto = "Test",
        StavJednani = stateCode
    };

    private static async Task<int> CreateDatedMeetingAsync(
        PmTrackerDbContext dbContext, int projectId, string stateCode, int number, DateTime date)
    {
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, stateCode, meetingNumber: number);
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = date;
        await dbContext.SaveChangesAsync();
        return meetingId;
    }

    /// <summary>Přímý zápis změny stavu do historie (a do záznamu) — stav, jaký by zanechalo dřívější uložení.</summary>
    private static async Task<int> AddStatusChangeAsync(
        PmTrackerDbContext dbContext, int recordId, bool toFinal, DateTime platiOd, int? jednaniId)
    {
        var running = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var done = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.StavUkoluId = toFinal ? done : running;
        var change = new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = toFinal ? running : done,
            NovyStav = toFinal ? done : running,
            DatumZmeny = new DateTime(2026, 10, 13, 7, 0, 0),
            PlatiOd = platiOd,
            JednaniId = jednaniId
        };
        dbContext.ZaznamHistorieStavuZaznamu.Add(change);
        await dbContext.SaveChangesAsync();
        return change.Id;
    }

    private static Task<int?> CurrentStatusAsync(PmTrackerDbContext dbContext, int recordId)
        => dbContext.ProjektoveZaznamy.AsNoTracking().Where(x => x.Id == recordId).Select(x => x.StavUkoluId).SingleAsync();

    private sealed record ArrangedTask(int ProjectId, int RecordId, CurrentUserContextViewModel User, string DoneStatusCode);

    private static async Task<ArrangedTask> ArrangeTaskAsync(PmTrackerDbContext dbContext, string marker)
    {
        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Admin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Owner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", marker);

        // Založení před všemi jednáními v testech, aby spodní mez (spec §4.4) nic nevyřadila.
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = RecordFoundedOn;
        await dbContext.SaveChangesAsync();

        var doneStatusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();

        return new ArrangedTask(
            projectId,
            recordId,
            IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true),
            doneStatusCode);
    }

    private static async Task<SaveRecordCommand> BuildEditCommandAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        string statusCode,
        string? stavPlatiK = null,
        DateTime? datumZalozeni = null)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).SingleAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).SingleAsync();

        return new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = datumZalozeni ?? record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu,
            StavPlatiK = stavPlatiK
        };
    }

    private sealed class PragueTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    }
}
