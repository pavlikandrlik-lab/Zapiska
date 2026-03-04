using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordDeleteDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordDeleteDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteRecord_ShouldCascadeRelatedData_AndWriteAudit()
    {
        var db = await _fixture.CreateDatabaseAsync("record_delete_cascade");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordDeleteAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordDeleteOwner");
        var collaboratorId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordDeleteCollaborator");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RECDEL");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RECDEL_SYS", adminId);
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", 870);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "CascadeDelete");

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        var externalTypeId = await dbContext.CiselnikTypuExternichOdkazu
            .Where(x => x.Kod == "PMP")
            .Select(x => x.Id)
            .FirstAsync();
        var scheduleTypeId = await dbContext.CiselnikHarmonogramTypu
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .Select(x => x.Id)
            .FirstAsync();
        var typeId = await dbContext.CiselnikTypuUkolu
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var statusId = await dbContext.ProjektoveZaznamy
            .Where(x => x.Id == recordId)
            .Select(x => x.StavUkoluId)
            .FirstAsync()
            ?? throw new InvalidOperationException("Test record is missing task state.");

        dbContext.Vyjadreni.Add(new VyjadreniEntity
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            AutorOsobaId = ownerId,
            TextVyjadreni = "Cascade comment",
            DatumVyjadreni = DateTime.UtcNow
        });
        dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            ZaznamId = recordId,
            TypOdkazuId = externalTypeId,
            Cislo = "PMP-870-1"
        });
        dbContext.ZaznamSpoluprace.Add(new ZaznamSpolupraceEntity
        {
            ZaznamId = recordId,
            OsobaId = collaboratorId
        });
        dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            ZaznamId = recordId,
            TypId = scheduleTypeId,
            HodnotaInt = 4,
            UpdatedAt = DateTime.UtcNow
        });
        dbContext.ZaznamHistorieZmenTypu.Add(new ZaznamHistorieZmenTypuEntity
        {
            ZaznamId = recordId,
            PuvodniTypId = typeId,
            NovyTypId = typeId,
            DatumZmeny = DateTime.UtcNow,
            ZmenilOsobaId = adminId
        });
        dbContext.ZaznamHistorieTerminu.Add(new ZaznamHistorieTerminuEntity
        {
            ZaznamId = recordId,
            PuvodniDatum = DateTime.Today,
            NoveDatum = DateTime.Today.AddDays(1),
            DatumZmeny = DateTime.UtcNow,
            Duvod = "cascade test"
        });
        dbContext.ZaznamHistorieVlastnik.Add(new ZaznamHistorieVlastnikEntity
        {
            ZaznamId = recordId,
            PuvodniVlastnik = ownerId,
            NovyVlastnik = ownerId,
            DatumZmeny = DateTime.UtcNow
        });
        dbContext.ZaznamHistorieSubsystem.Add(new ZaznamHistorieSubsystemEntity
        {
            ZaznamId = recordId,
            PuvodniSubsystem = subsystemId,
            NovySubsystem = subsystemId,
            DatumZmeny = DateTime.UtcNow
        });
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = statusId,
            NovyStav = statusId,
            DatumZmeny = DateTime.UtcNow
        });
        dbContext.ZaznamHistorieStavuProjektu.Add(new ZaznamHistorieStavuProjektuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = statusId,
            NovyStav = statusId,
            DatumZmeny = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);
        store.DeleteRecord(new DeleteRecordCommand
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            PotvrditSmazani = true
        }, currentUser);

        (await dbContext.ProjektoveZaznamy.AnyAsync(x => x.Id == recordId)).Should().BeFalse();
        (await dbContext.Vyjadreni.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamExterniOdkazy.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamSpoluprace.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHarmonogramHodnoty.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHistorieZmenTypu.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHistorieTerminu.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHistorieVlastnik.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHistorieSubsystem.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHistorieStavuZaznamu.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await dbContext.ZaznamHistorieStavuProjektu.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();

        var auditRow = await dbContext.AuthzAuditLog
            .AsNoTracking()
            .Where(x => x.EntityType == "projektove_zaznamy" && x.EntityId == recordId.ToString() && x.Action == "hard_delete")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        auditRow.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveRecord_ShouldReuseLowestAvailableInternalAndMeetingNumbers_AfterHardDelete()
    {
        var db = await _fixture.CreateDatabaseAsync("record_delete_reuse_numbers");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordReuseAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordReuseOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "REUSE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "REUSE_SYS", adminId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);

        var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
        project.PouzivatIdentJednani = true;
        await dbContext.SaveChangesAsync();

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", 870);
        var categoryName = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == "U")
            .Select(x => x.Nazev)
            .FirstAsync();
        var statusName = await dbContext.CiselnikStavuUkolu
            .OrderBy(x => x.Id)
            .Select(x => x.Nazev)
            .FirstAsync();
        var subsystemCode = await dbContext.Subsystemy
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .FirstAsync();
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var firstId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryName,
            Stav = statusName,
            Nazev = "Reuse 1",
            VlastnikId = ownerId,
            DatumZalozeni = DateTime.Today,
            TerminUkonceni = DateTime.Today.AddDays(5),
            Subsystem = subsystemCode,
            JednaniIdProCislo = meetingId
        }, currentUser);

        var secondId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryName,
            Stav = statusName,
            Nazev = "Reuse 2",
            VlastnikId = ownerId,
            DatumZalozeni = DateTime.Today,
            TerminUkonceni = DateTime.Today.AddDays(10),
            Subsystem = subsystemCode,
            JednaniIdProCislo = meetingId
        }, currentUser);

        var firstRecord = await dbContext.ProjektoveZaznamy.AsNoTracking().FirstAsync(x => x.Id == firstId);
        var secondRecord = await dbContext.ProjektoveZaznamy.AsNoTracking().FirstAsync(x => x.Id == secondId);
        firstRecord.CisloZaznamu.Should().Be(1);
        firstRecord.CisloViditelne.Should().Be("870-1");
        secondRecord.CisloZaznamu.Should().Be(2);
        secondRecord.CisloViditelne.Should().Be("870-2");

        store.DeleteRecord(new DeleteRecordCommand
        {
            ProjektId = projectId,
            ZaznamId = firstId,
            PotvrditSmazani = true
        }, currentUser);

        var thirdId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryName,
            Stav = statusName,
            Nazev = "Reuse 3",
            VlastnikId = ownerId,
            DatumZalozeni = DateTime.Today,
            TerminUkonceni = DateTime.Today.AddDays(15),
            Subsystem = subsystemCode,
            JednaniIdProCislo = meetingId
        }, currentUser);

        var thirdRecord = await dbContext.ProjektoveZaznamy.AsNoTracking().FirstAsync(x => x.Id == thirdId);
        thirdRecord.CisloZaznamu.Should().Be(1);
        thirdRecord.CisloViditelne.Should().Be("870-1");
    }

    [Fact]
    public async Task BuildZaznamEdit_ShouldReturnOnlyActiveOwnerCandidates_AndKeepLegacySelectedOwner()
    {
        var db = await _fixture.CreateDatabaseAsync("record_owner_candidates");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OwnerCandidateAdmin");
        var activeProjectMemberId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OwnerCandidateActiveProject");
        var activeSubsystemMemberId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OwnerCandidateActiveSubsystem");
        var legacyOwnerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OwnerCandidateLegacy");
        var outsiderId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OwnerCandidateOutsider");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "OWNRCAND");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "OWNRCAND_SYS", adminId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, activeProjectMemberId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, legacyOwnerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, activeSubsystemMemberId, SubsystemRoleCodes.Lead);

        var legacyMembership = await dbContext.ObsazeniProjektu
            .Where(x => x.ProjektId == projectId && x.OsobaId == legacyOwnerId && x.DatumOdebrani == null)
            .OrderByDescending(x => x.DatumPrirazeni)
            .FirstAsync();
        legacyMembership.DatumOdebrani = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, legacyOwnerId, subsystemId, "U", "LegacyOwner");

        var model = store.BuildZaznamEdit(recordId);
        var ownerIds = model.DostupniVlastnici.Select(x => x.OsobaId).ToArray();
        var collaborationIds = model.DostupniSpolupracovnici.Select(x => x.OsobaId).ToArray();

        ownerIds.Should().Contain(activeProjectMemberId);
        ownerIds.Should().Contain(activeSubsystemMemberId);
        ownerIds.Should().Contain(legacyOwnerId);
        ownerIds.Should().NotContain(outsiderId);

        collaborationIds.Should().Contain(activeProjectMemberId);
        collaborationIds.Should().Contain(activeSubsystemMemberId);
        collaborationIds.Should().NotContain(legacyOwnerId);
        collaborationIds.Should().NotContain(outsiderId);
    }
}
