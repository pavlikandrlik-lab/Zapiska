using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>A1 (2026-07-08): detail jednání nese u účastníků jejich aktivní role v projektu.</summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingAttendanceRolesDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;
    public MeetingAttendanceRolesDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BuildJednaniDetail_UcastCarriesActiveProjectRoles()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_attendance_roles");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttRoleAdmin");
        var memberId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttRoleMember");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "ATTROLE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ATTROLE_SYS", adminId);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, memberId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, memberId, SubsystemRoleCodes.Lead);

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9720);
        var detail = store.BuildJednaniDetail(meetingId);

        // Bez explicitní účasti → legacy fallback z aktivních členů; memberId tam je s rolemi.
        var member = detail.Ucast.SingleOrDefault(x => x.OsobaId == memberId);
        member.Should().NotBeNull();
        member!.AktivniRole.Should().NotBeEmpty("aktivní projektová i subsystémová role musí být vidět");
        member.AktivniRole.Should().Contain(r => r.Contains("ATTROLE_SYS"), "subsystémový label nese kód subsystému");
    }

    /// <summary>B6 (2026-07-09): pořadí = role skupina (Vlastník→Gestor→PM→Admin→zbytek),
    /// uvnitř skupiny příjmení→jméno. Nahrazuje surname-only řazení z 2026-07-09 rána.</summary>
    [Fact]
    public async Task BuildJednaniDetail_OrdersUcastAndCandidates_ByRoleGroupThenSurname()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_attendance_role_groups");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var seedAdmin = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "GrpSeedAdmin");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "GRPORD");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "GRPORD_SYS", seedAdmin);

        async Task<int> PersonAsync(string marker, string jmeno, string prijmeni)
        {
            var id = await IntegrationTestHelper.EnsurePersonAsync(dbContext, marker);
            var p = await dbContext.Osoby.SingleAsync(x => x.Id == id);
            p.Jmeno = jmeno; p.Prijmeni = prijmeni;
            await dbContext.SaveChangesAsync();
            return id;
        }

        // Křestní/příjmení volená PROTI skupinovému pořadí (skupina musí přebít abecedu):
        var vlastnik = await PersonAsync("GrpVlastnik", "Zdenek", "Žlutý");      // skupina 1
        var pm       = await PersonAsync("GrpPm",       "Cyril",  "Adamec");     // skupina 3
        var admin2   = await PersonAsync("GrpAdminB",   "Bora",   "Bílý");       // skupina 4
        var admin1   = await PersonAsync("GrpAdminA",   "Alan",   "Adam");       // skupina 4 (před Bílým)
        var subOnly  = await PersonAsync("GrpSubOnly",  "Aida",   "Aaron");      // skupina 5 (jen subsystém)

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, vlastnik, "VLASTNIK_PROJEKTU");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, pm, "PROJ_MAN");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, admin1, "ADM_PROJ");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, admin2, "ADM_PROJ");
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, subOnly, SubsystemRoleCodes.Lead);

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9722);
        var detail = store.BuildJednaniDetail(meetingId);

        var expected = new[] { vlastnik, pm, admin1, admin2, subOnly };
        detail.Ucast.Select(x => x.OsobaId).Where(id => expected.Contains(id))
            .Should().ContainInOrder(expected,
                "skupina (Vlastník→PM→Admin→zbytek) přebíjí příjmení; uvnitř Admin skupiny Adam < Bílý");
        detail.AvailableParticipantCandidates.Select(x => x.OsobaId).Where(id => expected.Contains(id))
            .Should().ContainInOrder(expected, "kandidáti drží stejné řazení");

        // Varianta s EXPLICITNÍ účastí (Ucast řádky) — normal path musí řadit stejně.
        var presentStateId = await dbContext.CiselnikStavuUcasti
            .OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        foreach (var osobaId in new[] { subOnly, admin2, vlastnik, admin1, pm }) // schválně rozházené
        {
            dbContext.Ucast.Add(new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = osobaId,
                StavUcastiId = presentStateId
            });
        }
        await dbContext.SaveChangesAsync();

        var detailWithUcast = store.BuildJednaniDetail(meetingId);
        detailWithUcast.Ucast.Select(x => x.OsobaId).Where(id => expected.Contains(id))
            .Should().ContainInOrder(expected, "explicitní účast řadí stejným triem (priorita z membership lookup)");
    }
}
