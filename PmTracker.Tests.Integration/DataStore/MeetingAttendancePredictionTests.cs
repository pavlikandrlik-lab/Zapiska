using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Předvyplnění účasti u nového jednání (2026-09-05) — průchod od SaveMeeting až do SQL.
/// Rozhodovací pravidla samotná hlídá AttendancePredictorTests v Unit projektu.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingAttendancePredictionTests
{
    private readonly SqlIntegrationFixture _fixture;

    public MeetingAttendancePredictionTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveMeeting_ShouldPrefillAttendance_FromClosedMeetingHistory()
    {
        var db = await _fixture.CreateDatabaseAsync("attendance_prediction");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PredictAdmin");
        var veteranId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PredictVeteran");
        var newcomerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PredictNewcomer");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PREDICT1");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(
            dbContext, projectId, veteranId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(
            dbContext, projectId, newcomerId, ProjectRoleCodes.ProjectManager);

        var onlineStateId = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "ONLINE")
            .Select(x => x.Id)
            .SingleAsync();
        var presentStateId = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "PRESENT")
            .Select(x => x.Id)
            .SingleAsync();

        // Historie veterána: 2× videokonference, 1× přítomen — vše u uzavřených jednání.
        var historyStates = new[] { onlineStateId, onlineStateId, presentStateId };
        for (var index = 0; index < historyStates.Length; index++)
        {
            var meetingId = await IntegrationTestHelper.CreateMeetingAsync(
                dbContext, projectId, "CLOSED", meetingNumber: 9601 + index);
            dbContext.Ucast.Add(new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = veteranId,
                StavUcastiId = historyStates[index]
            });
        }

        await dbContext.SaveChangesAsync();

        var newMeetingId = store.SaveMeeting(new SaveMeetingCommand
        {
            ProjektId = projectId,
            CisloJednani = 9610,
            DatumPlanovane = new DateTime(2026, 9, 10),
            CasZacatek = new TimeOnly(9, 0),
            Misto = "Zasedačka",
            StavJednani = "OPEN"
        }, currentUser);

        var attendance = await dbContext.Ucast
            .AsNoTracking()
            .Where(x => x.JednaniId == newMeetingId)
            .ToDictionaryAsync(x => x.OsobaId, x => x.StavUcastiId);

        attendance[veteranId].Should().Be(onlineStateId,
            "veterán byl na dvou ze tří uzavřených jednání přes videokonferenci");
        attendance[newcomerId].Should().Be(presentStateId,
            "osoba bez historie dostane výchozí stav jako dosud");
    }

    /// <summary>
    /// Odhad se uplatní jen při ZALOŽENÍ jednání (spec U5). Úprava hlavičky ani
    /// uzavření/otevření jednání nesmí docházku znovu předvyplnit — jinak by přepsaly
    /// ruční zásahy obsluhy a vrátily i účastníky, které obsluha ze seznamu odebrala.
    /// </summary>
    [Fact]
    public async Task EditingAndReopeningMeeting_ShouldNotRerunAttendancePrediction()
    {
        var db = await _fixture.CreateDatabaseAsync("attendance_prediction_once");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OnceAdmin");
        var veteranId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OnceVeteran");
        var droppedId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OnceDropped");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PREDICT2");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(
            dbContext, projectId, veteranId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(
            dbContext, projectId, droppedId, ProjectRoleCodes.ProjectManager);

        var onlineStateId = await ResolveAttendanceStateIdAsync(dbContext, "ONLINE");
        var absentStateId = await ResolveAttendanceStateIdAsync(dbContext, "ABSENT");

        var historyMeetingId = await IntegrationTestHelper.CreateMeetingAsync(
            dbContext, projectId, "CLOSED", meetingNumber: 9701);
        dbContext.Ucast.Add(new UcastEntity
        {
            JednaniId = historyMeetingId,
            OsobaId = veteranId,
            StavUcastiId = onlineStateId
        });
        await dbContext.SaveChangesAsync();

        var meetingId = store.SaveMeeting(new SaveMeetingCommand
        {
            ProjektId = projectId,
            CisloJednani = 9710,
            DatumPlanovane = new DateTime(2026, 9, 10),
            CasZacatek = new TimeOnly(9, 0),
            Misto = "Zasedačka",
            StavJednani = "OPEN"
        }, currentUser);

        // Obsluha zasáhne do předvyplněné docházky: veteránovi opraví stav a jednoho
        // účastníka ze seznamu úplně odebere.
        var veteranRow = await dbContext.Ucast
            .SingleAsync(x => x.JednaniId == meetingId && x.OsobaId == veteranId);
        veteranRow.StavUcastiId = absentStateId;
        dbContext.Ucast.RemoveRange(dbContext.Ucast
            .Where(x => x.JednaniId == meetingId && x.OsobaId == droppedId));
        await dbContext.SaveChangesAsync();

        // Úprava hlavičky jednání.
        store.SaveMeeting(new SaveMeetingCommand
        {
            Id = meetingId,
            ProjektId = projectId,
            CisloJednani = 9710,
            DatumPlanovane = new DateTime(2026, 9, 11),
            CasZacatek = new TimeOnly(10, 0),
            Misto = "Jiná zasedačka",
            StavJednani = "OPEN"
        }, currentUser);

        // Uzavření a znovuotevření jednání.
        store.SaveMeetingStatus(new SaveMeetingStatusCommand
        {
            JednaniId = meetingId,
            Stav = "CLOSED",
            UzavritJednani = true
        }, currentUser);
        store.SaveMeetingStatus(new SaveMeetingStatusCommand
        {
            JednaniId = meetingId,
            Stav = "OPEN",
            OtevritJednani = true
        }, currentUser);

        dbContext.ChangeTracker.Clear();
        var attendance = await dbContext.Ucast
            .AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .ToDictionaryAsync(x => x.OsobaId, x => x.StavUcastiId);

        attendance[veteranId].Should().Be(absentStateId,
            "ruční oprava obsluhy nesmí být přepsána odhadem při úpravě ani otevření jednání");
        attendance.Should().NotContainKey(droppedId,
            "odebraný účastník se nesmí vrátit — odhad běží jen při založení jednání (spec U5)");
    }

    private static async Task<int> ResolveAttendanceStateIdAsync(PmTrackerDbContext dbContext, string code)
        => await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == code)
            .Select(x => x.Id)
            .SingleAsync();
}
