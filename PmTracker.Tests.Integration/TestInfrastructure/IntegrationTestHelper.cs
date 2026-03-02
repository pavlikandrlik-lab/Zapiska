using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Integration.TestInfrastructure;

internal static class IntegrationTestHelper
{
    public static PmTrackerDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(60))
            .Options;

        return new PmTrackerDbContext(options);
    }

    public static SqlServerDataStore CreateDataStore(PmTrackerDbContext dbContext)
    {
        var textNormalizer = new TextNormalizer();
        var identityMatcher = new PersonIdentityMatcher(textNormalizer);
        var permissionEvaluation = new PermissionEvaluationService();
        var commentAuthorization = new CommentAuthorizationPolicy(permissionEvaluation);
        return new SqlServerDataStore(dbContext, textNormalizer, identityMatcher, commentAuthorization);
    }

    public static CurrentUserContextViewModel BuildUser(
        int osobaId,
        bool isSuperAdmin = false,
        IEnumerable<PermissionGrantViewModel>? grants = null,
        IEnumerable<int>? visibleProjectIds = null)
    {
        var grantList = grants?.ToList() ?? new List<PermissionGrantViewModel>();
        return new CurrentUserContextViewModel
        {
            OsobaId = osobaId,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "test.user@pmtracker.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = isSuperAdmin,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = visibleProjectIds?.Distinct().ToArray() ?? grantList.SelectMany(x => x.ProjectIds).Distinct().ToArray(),
            PermissionGrants = grantList
        };
    }

    public static PermissionGrantViewModel AllowProjectPermission(string key, int projectId)
    {
        return new PermissionGrantViewModel
        {
            PermissionKey = key,
            ScopeLevel = "PROJECT",
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjectIds = new[] { projectId }
        };
    }

    public static async Task<int> EnsurePersonAsync(PmTrackerDbContext dbContext, string marker)
    {
        var orgId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync();
        var orgUnitId = await dbContext.CiselnikOrganizacniCelky.Select(x => x.Id).FirstOrDefaultAsync();

        var email = $"{marker.ToLowerInvariant()}@pmtracker.test";
        var existingId = await dbContext.Osoby
            .Where(x => x.Email == email)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var person = new OsobaEntity
        {
            Jmeno = marker,
            Prijmeni = "Tester",
            Titul = "Ing.",
            Email = email,
            OrganizaceId = orgId,
            OrganizacniCelekId = orgUnitId
        };

        dbContext.Osoby.Add(person);
        await dbContext.SaveChangesAsync();
        return person.Id;
    }

    public static async Task<int> EnsureProjectAsync(PmTrackerDbContext dbContext, string marker)
    {
        var existing = await dbContext.Projekty
            .Where(x => x.Zkratka == marker)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            return existing.Value;
        }

        var statusId = await dbContext.CiselnikStavuProjektu
            .Where(x => x.Kod == "RUN")
            .Select(x => x.Id)
            .FirstAsync();

        var project = new ProjektEntity
        {
            Zkratka = marker,
            CelyNazev = $"{marker} Test Project",
            StavId = statusId
        };

        dbContext.Projekty.Add(project);
        await dbContext.SaveChangesAsync();
        return project.Id;
    }

    public static async Task<int> EnsureSubsystemAsync(PmTrackerDbContext dbContext, string marker, int leaderOsobaId)
    {
        var existing = await dbContext.Subsystemy
            .Where(x => x.Kod == marker)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            return existing.Value;
        }

        var subsystem = new SubsystemEntity
        {
            Kod = marker,
            Nazev = $"{marker} Subsystem"
        };

        dbContext.Subsystemy.Add(subsystem);
        await dbContext.SaveChangesAsync();
        return subsystem.Id;
    }

    public static async Task<int> EnsureProjectSubsystemAsync(PmTrackerDbContext dbContext, int projectId, int subsystemId)
    {
        var existing = await dbContext.ProjektSubsystemy
            .Where(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && x.DatumOdebrani == null)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            return existing.Value;
        }

        var entity = new ProjektSubsystemEntity
        {
            ProjektId = projectId,
            SubsystemId = subsystemId,
            DatumPrirazeni = DateTime.UtcNow
        };

        dbContext.ProjektSubsystemy.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    public static async Task EnsureActiveProjectRoleAssignmentAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        int osobaId,
        string roleCode)
    {
        var roleId = await dbContext.CiselnikRoliProjektu
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .FirstAsync();

        var exists = await dbContext.ObsazeniProjektu.AnyAsync(x =>
            x.ProjektId == projectId &&
            x.OsobaId == osobaId &&
            x.RoleId == roleId &&
            x.DatumOdebrani == null);

        if (exists)
        {
            return;
        }

        dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            ProjektId = projectId,
            OsobaId = osobaId,
            RoleId = roleId,
            DatumPrirazeni = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    public static async Task EnsureActiveSubsystemRoleAssignmentAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        int subsystemId,
        int osobaId,
        string roleCode)
    {
        var projectSubsystemId = await EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        var roleId = await dbContext.CiselnikRoliSubsystemu
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .FirstAsync();

        var exists = await dbContext.ObsazeniSubsystemuProjektu.AnyAsync(x =>
            x.ProjektSubsystemId == projectSubsystemId &&
            x.OsobaId == osobaId &&
            x.RoleSubsystemuId == roleId &&
            x.DatumOdebrani == null);

        if (exists)
        {
            return;
        }

        dbContext.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            ProjektSubsystemId = projectSubsystemId,
            OsobaId = osobaId,
            RoleSubsystemuId = roleId,
            DatumPrirazeni = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    public static async Task<int> EnsureRecordAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        int ownerOsobaId,
        int subsystemId,
        string categoryCode,
        string marker)
    {
        var categoryId = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == categoryCode)
            .Select(x => x.Id)
            .FirstAsync();

        var taskStateId = await dbContext.CiselnikStavuUkolu
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var schemaVersion = await dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .Select(x => (int?)x.Verze)
            .FirstOrDefaultAsync() ?? 1;

        var maxNumber = await dbContext.ProjektoveZaznamy
            .Where(x => x.ProjektId == projectId)
            .Select(x => (int?)x.CisloZaznamu)
            .MaxAsync() ?? 0;

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = projectId,
            KategorieId = categoryId,
            AktualniTypUkoluId = null,
            StavUkoluId = taskStateId,
            CisloZaznamu = maxNumber + 1,
            Nazev = $"{marker} Record",
            Popis = $"{marker} Popis",
            VlastnikId = ownerOsobaId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today.AddDays(30),
            SubsystemId = subsystemId,
            HarmonogramSablonaVerze = schemaVersion
        };

        dbContext.ProjektoveZaznamy.Add(record);
        await dbContext.SaveChangesAsync();

        return record.Id;
    }

    public static async Task<int> CreateMeetingAsync(
        PmTrackerDbContext dbContext,
        int projectId,
        string stateCode,
        int meetingNumber,
        int? lockByOsobaId = null)
    {
        var stateId = await dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == stateCode)
            .Select(x => x.Id)
            .FirstAsync();

        var meeting = new JednaniEntity
        {
            ProjektId = projectId,
            CisloJednani = meetingNumber,
            DatumPlanovane = DateTime.Today,
            CasZacatek = new TimeOnly(9, 0),
            Misto = "Test",
            StavJednaniId = stateId,
            UzamklOsobaId = lockByOsobaId
        };

        dbContext.Jednani.Add(meeting);
        await dbContext.SaveChangesAsync();

        return meeting.Id;
    }
}
