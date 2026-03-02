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
        IEnumerable<PermissionGrantViewModel>? grants = null)
    {
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
            PermissionGrants = grants?.ToList() ?? new List<PermissionGrantViewModel>()
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
            var row = await dbContext.Subsystemy.FirstAsync(x => x.Id == existing.Value);
            row.VedouciOsobaId = leaderOsobaId;
            await dbContext.SaveChangesAsync();
            return row.Id;
        }

        var subsystem = new SubsystemEntity
        {
            Kod = marker,
            Nazev = $"{marker} Subsystem",
            VedouciOsobaId = leaderOsobaId
        };

        dbContext.Subsystemy.Add(subsystem);
        await dbContext.SaveChangesAsync();
        return subsystem.Id;
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
            SubsystemId = subsystemId
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
