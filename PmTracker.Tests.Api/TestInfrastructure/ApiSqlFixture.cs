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

        // F4 fix 2026-04-23: warm-up klienta spustí host → PermissionSeeder.SeedAsync upsert
        // všech klíčů (včetně nových per-action) před tím, než testy začnou hledat ID v DB.
        // Bez warm-upu Grant* helpery selhaly na klíčích zavedených F1 redesignem, protože
        // bootstrap SQL obsahuje jen pre-redesign permission sadu.
        using var warmupClient = Factory.CreateClient();
        _ = await warmupClient.GetAsync("/");
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

    public Task<int> EnsurePersonAsync(string marker)
        => EnsurePersonAsync(marker, jmeno: marker, prijmeni: "Api");

    /// <summary>
    /// Overload s explicitním jménem/příjmením — pro testy, kde záleží na řazení/zobrazení
    /// jména (např. owner filter „Příjmení Jméno" řazený dle příjmení).
    /// </summary>
    public async Task<int> EnsurePersonAsync(string marker, string jmeno, string prijmeni)
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
            Jmeno = jmeno,
            Prijmeni = prijmeni,
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
            Nazev = $"{marker} API Subsystem"
        };

        dbContext.Subsystemy.Add(subsystem);
        await dbContext.SaveChangesAsync();
        return subsystem.Id;
    }

    /// <summary>
    /// A4 (2026-07-09): přiřadí osobě roli vedoucího subsystému v projektu (projekt_subsystemy +
    /// obsazeni_subsystemu_projektu). Potřebné pro lead-gated stránky (návrhy záznamů).
    /// </summary>
    public async Task EnsureSubsystemLeadAsync(int projectId, int subsystemId, int osobaId)
    {
        await using var dbContext = CreateDbContext();

        var projectSubsystemId = await dbContext.ProjektSubsystemy
            .Where(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && x.DatumOdebrani == null)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (!projectSubsystemId.HasValue)
        {
            var link = new ProjektSubsystemEntity
            {
                ProjektId = projectId,
                SubsystemId = subsystemId,
                Poradi = 1,
                DatumPrirazeni = DateTime.UtcNow
            };
            dbContext.ProjektSubsystemy.Add(link);
            await dbContext.SaveChangesAsync();
            projectSubsystemId = link.Id;
        }

        var leadRoleId = await dbContext.CiselnikRoliSubsystemu
            .Where(x => x.Kod == PmTracker.Web.Models.ViewModels.SubsystemRoleCodes.Lead)
            .Select(x => x.Id)
            .FirstAsync();

        var alreadyAssigned = await dbContext.ObsazeniSubsystemuProjektu
            .AnyAsync(x => x.ProjektSubsystemId == projectSubsystemId.Value
                && x.OsobaId == osobaId
                && x.RoleSubsystemuId == leadRoleId
                && x.DatumOdebrani == null);
        if (!alreadyAssigned)
        {
            dbContext.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
            {
                ProjektSubsystemId = projectSubsystemId.Value,
                OsobaId = osobaId,
                RoleSubsystemuId = leadRoleId,
                DatumPrirazeni = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>B3/B4 (2026-07-09): pending návrh založení záznamu pro render testy detailu.</summary>
    public async Task<int> EnsurePendingCreateProposalAsync(int projectId, int subsystemId, int authorOsobaId)
    {
        await using var dbContext = CreateDbContext();
        var existing = await dbContext.ZaznamNavrhy
            .Where(x => x.ProjektId == projectId && x.Stav == "PENDING" && x.TypNavrhu == "CREATE_RECORD")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var subsystemKod = await dbContext.Subsystemy
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .FirstAsync();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            ProposalType = "CREATE_RECORD",
            CreateRecord = new
            {
                ProjektId = projectId,
                Kategorie = "U",
                Stav = "OPEN",
                Nazev = "Api pending návrh",
                VlastnikId = authorOsobaId,
                DatumZalozeni = DateTime.Today,
                TerminUkonceni = DateTime.Today.AddDays(30),
                Subsystem = subsystemKod,
                VybraniSpolupracovniciIds = Array.Empty<int>(),
                ExterniVazby = Array.Empty<object>(),
                HarmonogramHodnoty = Array.Empty<object>()
            }
        });

        var proposal = new ZaznamNavrhEntity
        {
            ProjektId = projectId,
            SubsystemId = subsystemId,
            TypNavrhu = "CREATE_RECORD",
            Stav = "PENDING",
            PayloadJson = payload,
            CreatedByOsobaId = authorOsobaId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ZaznamNavrhy.Add(proposal);
        await dbContext.SaveChangesAsync();
        return proposal.Id;
    }

    /// <summary>A1 (2026-07-09): otevřené jednání pro render testy detailu (vzor Integration CreateMeetingAsync).</summary>
    public async Task<int> EnsureMeetingAsync(int projectId, int meetingNumber = 9800)
    {
        await using var dbContext = CreateDbContext();

        var existing = await dbContext.Jednani
            .Where(x => x.ProjektId == projectId && x.CisloJednani == meetingNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var stateId = await dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == "OPEN")
            .Select(x => x.Id)
            .FirstAsync();

        var meeting = new JednaniEntity
        {
            ProjektId = projectId,
            CisloJednani = meetingNumber,
            DatumPlanovane = DateTime.Today,
            CasZacatek = new TimeOnly(9, 0),
            Misto = "Api test",
            StavJednaniId = stateId
        };
        dbContext.Jednani.Add(meeting);
        await dbContext.SaveChangesAsync();
        return meeting.Id;
    }

    public async Task<int> EnsureRecordAsync(int projectId, int ownerOsobaId, int subsystemId, string categoryCode, string marker)
    {
        await using var dbContext = CreateDbContext();

        var categoryId = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == categoryCode)
            .Select(x => x.Id)
            .FirstAsync();
        var stateId = await dbContext.CiselnikStavuUkolu.Select(x => x.Id).FirstAsync();
        var hasProjectSubsystem = await dbContext.ProjektSubsystemy
            .AnyAsync(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue);
        if (!hasProjectSubsystem)
        {
            dbContext.ProjektSubsystemy.Add(new ProjektSubsystemEntity
            {
                ProjektId = projectId,
                SubsystemId = subsystemId,
                DatumPrirazeni = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var number = (await dbContext.ProjektoveZaznamy.Where(x => x.ProjektId == projectId).Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;

        var row = new ProjektovyZaznamEntity
        {
            ProjektId = projectId,
            KategorieId = categoryId,
            StavUkoluId = stateId,
            CisloZaznamu = number,
            Nazev = marker,
            Cil = marker,
            Popis = marker,
            VlastnikId = ownerOsobaId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today.AddDays(30),
            SubsystemId = subsystemId
        };

        dbContext.ProjektoveZaznamy.Add(row);
        await dbContext.SaveChangesAsync();
        return row.Id;
    }

    /// <summary>Datum-model seed: plán pro všech 10 kroků (týdenní rozestup od
    /// <paramref name="startDate"/>), skutečnost jen pro kroky v <paramref name="actualSteps"/>.
    /// Sdílený pro harmonogram render testy (dřív privátní v ProjectHarmonogramRenderTests).</summary>
    public async Task SeedDatumScheduleAsync(int recordId, DateTime startDate, IReadOnlySet<int> actualSteps)
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            var plan = startDate.Date.AddDays(poradi * 7);
            DateTime? actual = actualSteps.Contains(poradi) ? plan.AddDays(2) : null;
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = plan,
                SkutecnostDatum = actual,
                SkutecnostZdroj = actual.HasValue ? (byte)2 : (byte)0,
                SkutecnostRezim = 2,
                UpdatedAt = now
            });
        }
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Datum založení záznamu. Plán kroku 1 se počítá od založení (plan(0) = start), takže
    /// harmonogram nasetovaný před ním (SeedDatumScheduleAsync s minulým startem) by měl
    /// kroky před založením oříznuté na nulovou délku.
    /// </summary>
    public async Task SetRecordFoundingDateAsync(int recordId, DateTime datumZalozeni)
    {
        await using var dbContext = CreateDbContext();
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = datumZalozeni.Date;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Projektové (viditelné) číslo záznamu — drobečky i karty ho používají místo databázového Id.</summary>
    public async Task<string> GetRecordVisibleNumberAsync(int recordId)
    {
        await using var dbContext = CreateDbContext();
        var row = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => new { x.CisloViditelne, x.CisloZaznamu })
            .FirstAsync();
        return string.IsNullOrWhiteSpace(row.CisloViditelne) ? row.CisloZaznamu.ToString() : row.CisloViditelne;
    }

    /// <summary>
    /// Skutečnost kroku vytěžená z vyjádření: externí odkaz + aktivní vazba na krok.
    /// Read-only zobrazení z ní odvozuje odkaz „odkud skutečnost pochází".
    /// </summary>
    public async Task<int> SeedHarvestedActualAsync(int projectId, int recordId, int poradi)
    {
        await using var dbContext = CreateDbContext();

        var typId = await dbContext.CiselnikTypuExternichOdkazu.Select(x => x.Id).FirstAsync();
        var cislo = $"{900000 + recordId}";
        var odkaz = await dbContext.ZaznamExterniOdkazy
            .FirstOrDefaultAsync(x => x.ZaznamId == recordId && x.Cislo == cislo);
        if (odkaz is null)
        {
            odkaz = new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = typId,
                Cislo = cislo
            };
            dbContext.ZaznamExterniOdkazy.Add(odkaz);
            await dbContext.SaveChangesAsync();
        }

        var hasBinding = await dbContext.VyjadreniVazby
            .AnyAsync(x => x.ZaznamId == recordId && x.Poradi == (byte)poradi && x.Stav == (byte)VazbaStav.Active);
        if (!hasBinding)
        {
            dbContext.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                ExterniOdkazId = odkaz.Id,
                HotVyjadreniId = 500000 + recordId,
                DatumVyjadreni = DateTime.UtcNow.Date.AddDays(-5),
                Source = 1,
                Stav = (byte)VazbaStav.Active,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        // Krok musí být označen jako vytěžený automatem, jinak jde buňka jinou větví.
        var krok = await dbContext.ZaznamHarmonogramKroky
            .FirstAsync(x => x.ZaznamId == recordId && x.Poradi == (byte)poradi);
        krok.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;
        await dbContext.SaveChangesAsync();

        return odkaz.Id;
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

    public async Task EnsureProjectTeamMemberAsync(int projectId, int osobaId)
    {
        await using var dbContext = CreateDbContext();

        var roleId = await dbContext.CiselnikRoliProjektu
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var existing = await dbContext.ObsazeniProjektu
            .FirstOrDefaultAsync(x => x.ProjektId == projectId && x.OsobaId == osobaId);

        if (existing is not null)
        {
            if (existing.DatumOdebrani.HasValue)
            {
                existing.DatumOdebrani = null;
                await dbContext.SaveChangesAsync();
            }

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

    /// <summary>
    /// Authz úklid (2026-07-14): přiřadí osobě konkrétní projektovou roli dle Kod
    /// (CiselnikRoliProjektu → ObsazeniProjektu). Na rozdíl od EnsureProjectTeamMemberAsync
    /// (první role v číselníku) je deterministická — pro testy per-projektových authz grantů.
    /// Vzor: IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync.
    /// </summary>
    public async Task EnsureProjectRoleAssignmentAsync(int projectId, int osobaId, string roleCode)
    {
        await using var dbContext = CreateDbContext();

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
}
