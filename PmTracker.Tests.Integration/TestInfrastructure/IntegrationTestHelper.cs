using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql;

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

    public static IntegrationTestDataStore CreateDataStore(PmTrackerDbContext dbContext, TimeProvider? timeProvider = null)
    {
        return new IntegrationTestDataStore(BuildServiceProvider(dbContext, timeProvider));
    }

    public static IExportTemplateUseCase CreateExportTemplateUseCase(PmTrackerDbContext dbContext, TimeProvider? timeProvider = null)
    {
        return BuildServiceProvider(dbContext, timeProvider).GetRequiredService<IExportTemplateUseCase>();
    }

    public static ISettingsAuthzQueries CreateSettingsAuthzQueries(PmTrackerDbContext dbContext)
    {
        return BuildServiceProvider(dbContext).GetRequiredService<ISettingsAuthzQueries>();
    }

    public static ISettingsAuthzCommands CreateSettingsAuthzCommands(PmTrackerDbContext dbContext, TimeProvider? timeProvider = null)
        => BuildServiceProvider(dbContext, timeProvider).GetRequiredService<ISettingsAuthzCommands>();

    private static IServiceProvider BuildServiceProvider(PmTrackerDbContext dbContext, TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PmTracker"] = "Server=(localdb)\\mssqllocaldb;Database=PmTracker.Tests;Trusted_Connection=True;TrustServerCertificate=True;",
                ["PmTrackerData:Provider"] = "SqlServer",
                ["PmTrackerData:SqlServer:ConnectionStringName"] = "PmTracker",
                ["PmTrackerData:SqlServer:CommandTimeoutSeconds"] = "30"
            })
            .Build();
        var environment = new TestWebHostEnvironment();

        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddPmTrackerDataStore(configuration);
        // F8 A1 fix 2026-04-23: IAuthorizationService (per-action authz gate) je v produkci
        // registrován v Program.cs, ne v AddPmTrackerDataStore. UserContextResolver ho má
        // v konstruktoru, takže bez tohoto registrace by fixture DI container při resolve
        // UserContextResolver exploduje („Unable to resolve service..."). Stejně tak
        // IHttpContextAccessor potřebuje autorizační audit vrstva.
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        // ServiceDesk dotazy jsou v produkci registrované v Program.cs (ne v AddPmTrackerDataStore).
        // ExterniOdkazValidator (v data-store grafu) je vyžaduje → ve fixture registrujeme
        // disabled stub (Ticketing vypnutý, vrací prázdno) + default TicketingOptions.
        services.AddScoped<IVyjadreniQueryService, DisabledVyjadreniQueryService>();
        services.AddScoped<PmTracker.Web.Services.Schedules.IHarmonogramSkutecnostSyncService,
            PmTracker.Web.Services.Schedules.HarmonogramSkutecnostSyncService>();
        services.AddSingleton(dbContext);
        services.AddSingleton(timeProvider ?? TimeProvider.System);

        return services.BuildServiceProvider();
    }

    public static CurrentUserContextViewModel BuildUser(
        int osobaId,
        bool isSuperAdmin = false,
        IEnumerable<PermissionGrantViewModel>? grants = null,
        IEnumerable<int>? visibleProjectIds = null,
        IEnumerable<int>? deletedProjectIds = null)
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
            DeletedProjectIds = deletedProjectIds?.Distinct().ToArray() ?? Array.Empty<int>(),
            Authorization = BuildSnapshot(isSuperAdmin, grantList)
        };
    }

    /// <summary>
    /// Převede seznam <see cref="PermissionGrantViewModel"/> na <see cref="AuthorizationSnapshot"/>
    /// pro testovací účely.
    /// Pouze IsAllowed=true granty se mapují do snapshotu.
    /// ScopeLevel=GLOBAL nebo ScopeMode=ALL → GlobalPermissions.
    /// ScopeMode=INCLUDE + ProjectIds → PerProjectPermissions.
    /// </summary>
    private static AuthorizationSnapshot BuildSnapshot(bool isSuperAdmin, IReadOnlyList<PermissionGrantViewModel> grants)
    {
        var allowed = grants.Where(g => g.IsAllowed).ToList();

        var globalKeys = allowed
            .Where(g =>
                string.Equals(g.ScopeLevel, "GLOBAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(g.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.PermissionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var perProject = allowed
            .Where(g =>
                string.Equals(g.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(g.ScopeLevel, "GLOBAL", StringComparison.OrdinalIgnoreCase))
            .SelectMany(g => g.ProjectIds.Select(pid => (pid, g.PermissionKey)))
            .GroupBy(x => x.pid)
            .ToDictionary(
                grp => grp.Key,
                grp => (IReadOnlySet<string>)new HashSet<string>(grp.Select(x => x.PermissionKey), StringComparer.OrdinalIgnoreCase));

        return new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: globalKeys,
            PerProjectPermissions: perProject,
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            // Helper nemá subsystémovou dimenzi → všechny project-scope granty jsou „přímé".
            PerProjectDirectPermissions: perProject);
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
            Poradi = ((await dbContext.ProjektSubsystemy
                .Where(x => x.ProjektId == projectId && x.DatumOdebrani == null)
                .Select(x => (int?)x.Poradi)
                .MaxAsync()) ?? 0) + 1,
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
            .Where(x => !x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync()
            ?? await dbContext.CiselnikStavuUkolu
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
            Cil = $"{marker} Cíl",
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
