using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Common;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.TestInfrastructure;

[CollectionDefinition(CollectionName)]
public sealed class ApiSqlCollection : ICollectionFixture<ApiSqlFixture>
{
    public const string CollectionName = "api-sql";
}

public sealed class ApiSqlFixture : IAsyncLifetime
{
    private readonly SqlServerTestDatabaseManager _databaseManager = new();

    public TestDatabaseHandle Database { get; private set; } = null!;

    public PmTrackerWebAppFactory Factory { get; private set; } = null!;

    public int AdminOsobaId => Database.AdminOsobaId;

    public async Task InitializeAsync()
    {
        await _databaseManager.StartAsync();
        Database = await _databaseManager.CreateInitializedDatabaseAsync("api", includeSeed: true);
        Factory = new PmTrackerWebAppFactory(Database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        await _databaseManager.DisposeAsync();
    }

    public PmTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(Database.ConnectionString, sql => sql.CommandTimeout(60))
            .Options;

        return new PmTrackerDbContext(options);
    }

    public async Task<int> EnsurePersonAsync(string marker)
    {
        await using var dbContext = CreateDbContext();

        var email = $"{marker.ToLowerInvariant()}@pmtracker.test";
        var existing = await dbContext.Osoby
            .Where(x => x.Email == email)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            return existing.Value;
        }

        var orgId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync();
        var orgUnitId = await dbContext.CiselnikOrganizacniCelky.Select(x => x.Id).FirstOrDefaultAsync();

        var person = new OsobaEntity
        {
            Jmeno = marker,
            Prijmeni = "Api",
            Titul = "Ing.",
            Email = email,
            OrganizaceId = orgId,
            OrganizacniCelekId = orgUnitId
        };

        dbContext.Osoby.Add(person);
        await dbContext.SaveChangesAsync();
        return person.Id;
    }

    public async Task<int> EnsureProjectAsync(string marker)
    {
        await using var dbContext = CreateDbContext();

        var existing = await dbContext.Projekty
            .Where(x => x.Zkratka == marker)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            return existing.Value;
        }

        var statusId = await dbContext.CiselnikStavuProjektu.Where(x => x.Kod == "RUN").Select(x => x.Id).FirstAsync();
        var project = new ProjektEntity
        {
            Zkratka = marker,
            CelyNazev = $"{marker} API Project",
            StavId = statusId
        };

        dbContext.Projekty.Add(project);
        await dbContext.SaveChangesAsync();
        return project.Id;
    }

    public async Task<int> EnsureSubsystemAsync(string marker, int leaderOsobaId)
    {
        await using var dbContext = CreateDbContext();

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
            Nazev = $"{marker} API Subsystem",
            VedouciOsobaId = leaderOsobaId
        };

        dbContext.Subsystemy.Add(subsystem);
        await dbContext.SaveChangesAsync();
        return subsystem.Id;
    }

    public async Task<int> EnsureRecordAsync(int projectId, int ownerOsobaId, int subsystemId, string categoryCode, string marker)
    {
        await using var dbContext = CreateDbContext();

        var categoryId = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == categoryCode)
            .Select(x => x.Id)
            .FirstAsync();
        var stateId = await dbContext.CiselnikStavuUkolu.Select(x => x.Id).FirstAsync();
        var activeSchemaVersion = await dbContext.HarmonogramSablony
            .Where(x => x.IsAktivni)
            .OrderByDescending(x => x.Verze)
            .Select(x => (int?)x.Verze)
            .FirstOrDefaultAsync()
            ?? 1;
        var number = (await dbContext.ProjektoveZaznamy.Where(x => x.ProjektId == projectId).Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;

        var row = new ProjektovyZaznamEntity
        {
            ProjektId = projectId,
            KategorieId = categoryId,
            StavUkoluId = stateId,
            CisloZaznamu = number,
            Nazev = marker,
            Popis = marker,
            VlastnikId = ownerOsobaId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today.AddDays(30),
            SubsystemId = subsystemId,
            HarmonogramSablonaVerze = activeSchemaVersion
        };

        dbContext.ProjektoveZaznamy.Add(row);
        await dbContext.SaveChangesAsync();
        return row.Id;
    }

    public async Task<int> CreateMeetingAsync(int projectId, string stateCode, int meetingNumber)
    {
        await using var dbContext = CreateDbContext();

        var stateId = await dbContext.CiselnikStavuJednani.Where(x => x.Kod == stateCode).Select(x => x.Id).FirstAsync();

        var meeting = new JednaniEntity
        {
            ProjektId = projectId,
            CisloJednani = meetingNumber,
            DatumPlanovane = DateTime.Today,
            CasZacatek = new TimeOnly(10, 0),
            Misto = "API",
            StavJednaniId = stateId
        };

        dbContext.Jednani.Add(meeting);
        await dbContext.SaveChangesAsync();
        return meeting.Id;
    }
}
