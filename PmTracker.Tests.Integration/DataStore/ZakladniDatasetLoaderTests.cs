using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using Xunit;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ZakladniDatasetLoaderTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ZakladniDatasetLoaderTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LoadAsync_LoadsProjectRecordsBoundedByPeriodEnd()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_loader");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DatasetOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "DATASET1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "DATASET1_SYS", personId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        // EnsureRecordAsync nastaví DatumZalozeni=Today; pro test filtru období je přepíšeme:
        // jeden záznam 2025-03 (v období), druhý 2026-01 (po konci období Obdobi.Rok(2025) → vyloučen).
        var inPeriodId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "InPeriod");
        var futureId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "FutureCreated");
        var inPeriod = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == inPeriodId);
        inPeriod.DatumZalozeni = new DateTime(2025, 3, 1);
        var future = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == futureId);
        future.DatumZalozeni = new DateTime(2026, 1, 5);
        await dbContext.SaveChangesAsync();

        var loader = Loader(dbContext);
        var dataset = await loader.LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.Records.Should().ContainSingle(r => r.DatumZalozeni == new DateTime(2025, 3, 1));
        dataset.Records.Should().NotContain(r => r.DatumZalozeni.Year == 2026);
        dataset.Subsystemy.Should().Contain(s => s.Id == subsystemId);
        dataset.Stavy.Should().NotBeEmpty();
        dataset.ProjektNazev.Should().NotBeNullOrWhiteSpace("hlavička reportu = název projektu");
    }

    [Fact]
    public async Task LoadAsync_FillsDatumDokonceni_FromLastFinalStateTransition()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_dokonceni");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DokonceniOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "DOKONCENI1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "DOKONCENI1_SYS", personId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "Dokonceno");
        // EnsureRecordAsync nastaví DatumZalozeni=Today (mimo období 2025) → posuneme do období.
        var seededRecord = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == recordId);
        seededRecord.DatumZalozeni = new DateTime(2025, 3, 1);
        await dbContext.SaveChangesAsync();

        var finalStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        // PuvodniStav má FK na ciselnik_stavu_ukolu → použij existující (non-final) stav.
        var nonFinalStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => !x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var dokonceniDatum = new DateTime(2025, 6, 15);
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = nonFinalStateId,
            NovyStav = finalStateId,
            // Uloženo o měsíc později, platí k 15.6. — dashboard bere datum platnosti (spec 2026-10-13 §4.9).
            DatumZmeny = new DateTime(2025, 7, 20, 8, 0, 0),
            PlatiOd = dokonceniDatum
        });
        await dbContext.SaveChangesAsync();

        var loader = Loader(dbContext);
        var dataset = await loader.LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.Records.Should().Contain(r => r.Id == recordId && r.DatumDokonceni == dokonceniDatum);
    }

    [Fact]
    public async Task Loader_nacte_kategorii_a_vlastnika()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_kategorie");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "KategorieOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "KATEGORIE1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "KATEGORIE1_SYS", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        var ukolId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Ukol");
        var infoId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "INFO", "Informace");

        var dataset = await Loader(dbContext, dnes: new DateTime(2026, 5, 4, 10, 30, 0))
            .LoadAsync(projectId, Obdobi.Rok(DateTime.Today.Year), CancellationToken.None);

        var ukolKategorie = await dbContext.CiselnikKategoriiZaznamu.Where(c => c.Kod == "U").Select(c => c.Id).SingleAsync();
        dataset.TaskCategoryIds.Should().Contain(ukolKategorie);
        dataset.Records.Should().Contain(r => r.Id == ukolId && r.KategorieId == ukolKategorie && r.VlastnikId == ownerId);
        dataset.Ukoly.Select(r => r.Id).Should().Contain(ukolId).And.NotContain(infoId, "Informace nemá stav úkolu");
        dataset.Vlastnici.Should().Contain(v => v.Id == ownerId && v.Jmeno.Contains("KategorieOwner"));
        dataset.Dnes.Should().Be(new DateTime(2026, 5, 4, 10, 30, 0));
    }

    [Fact]
    public async Task Loader_jednani_a_ucasti_v_obdobi()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_jednani");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var osobaA = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "JednaniA");
        var osobaB = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "JednaniB");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "JEDNANI1");

        var vObdobi = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 1);
        var vPredchozim = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 2);
        var mimo = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 3);
        await NastavDatum(dbContext, vObdobi, new DateTime(2025, 6, 10));
        await NastavDatum(dbContext, vPredchozim, new DateTime(2024, 11, 5));
        await NastavDatum(dbContext, mimo, new DateTime(2023, 6, 1));

        var present = await StavUcasti(dbContext, "PRESENT");
        var jiny = await dbContext.CiselnikStavuUcasti.Where(x => x.Kod != "PRESENT").Select(x => x.Id).FirstAsync();
        dbContext.Ucast.AddRange(
            new UcastEntity { JednaniId = vObdobi, OsobaId = osobaA, StavUcastiId = present },
            new UcastEntity { JednaniId = vObdobi, OsobaId = osobaB, StavUcastiId = jiny },
            new UcastEntity { JednaniId = mimo, OsobaId = osobaA, StavUcastiId = present });
        await dbContext.SaveChangesAsync();

        var dataset = await Loader(dbContext).LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.Jednani.Select(j => j.Id).Should().BeEquivalentTo([vObdobi, vPredchozim], "období + předchozí období kvůli trendu");
        dataset.Jednani.Should().Contain(j => j.Id == vObdobi && j.Datum == new DateTime(2025, 6, 10));
        dataset.Ucasti.Should().BeEquivalentTo(new[]
        {
            new DatasetUcast(vObdobi, true),
            new DatasetUcast(vObdobi, false),
        });
    }

    [Fact]
    public async Task Loader_bez_is_ServiceDesk_null()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_bez_is");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "BEZIS1");

        var prodleni = new PevneProdleni(ProdleniPrehled.Prazdny(ProdleniStavDat.Ok, 1, "FIS"));
        var dataset = await Loader(dbContext, prodleni).LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.ServiceDesk.Should().BeNull();
        prodleni.Volani.Should().Be(0, "bez napojení na IS se ServiceDesk nevolá");
    }

    [Fact]
    public async Task Loader_s_is_nacte_slice_a_pri_vypadku_zachova_stav()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_s_is");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SIS1");
        var projekt = await dbContext.Projekty.SingleAsync(p => p.Id == projectId);
        projekt.ServiceDeskInfoSystemId = 1;
        await dbContext.SaveChangesAsync();

        var ok = await Loader(dbContext, new PevneProdleni(ProdleniPrehled.Prazdny(ProdleniStavDat.Ok, 1, "FIS")))
            .LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);
        ok.ServiceDesk.Should().NotBeNull();
        ok.ServiceDesk!.Prodleni.Stav.Should().Be(ProdleniStavDat.Ok);

        var vypadek = await Loader(dbContext, new PevneProdleni(ProdleniPrehled.Prazdny(ProdleniStavDat.Nedostupne, 1, "FIS")))
            .LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);
        vypadek.ServiceDesk.Should().NotBeNull("sekce ukáže chybovou kartu");
        vypadek.ServiceDesk!.Prodleni.Stav.Should().Be(ProdleniStavDat.Nedostupne);

        var vypnuto = await Loader(dbContext, new PevneProdleni(ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, 1, "FIS")))
            .LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);
        vypnuto.ServiceDesk.Should().BeNull("bez databáze ServiceDesku sekce není");
    }

    private static ZakladniDatasetLoader Loader(Web.Data.PmTrackerDbContext dbContext, IProdleniService? prodleni = null, DateTime? dnes = null)
    {
        var time = new PevnyCas(new DateTimeOffset(dnes ?? new DateTime(2026, 1, 15, 12, 0, 0), TimeSpan.Zero));
        return new ZakladniDatasetLoader(dbContext,
            prodleni ?? new PevneProdleni(ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, null, null)),
            new DisabledProdleniQueryService(), new DisabledInformacniSystemQueryService(), time,
            NullLogger<ZakladniDatasetLoader>.Instance);
    }

    private static async Task NastavDatum(Web.Data.PmTrackerDbContext dbContext, int jednaniId, DateTime datum)
    {
        var j = await dbContext.Jednani.SingleAsync(x => x.Id == jednaniId);
        j.DatumPlanovane = datum;
        await dbContext.SaveChangesAsync();
    }

    private static Task<int> StavUcasti(Web.Data.PmTrackerDbContext dbContext, string kod)
        => dbContext.CiselnikStavuUcasti.Where(x => x.Kod == kod).Select(x => x.Id).FirstAsync();

    private sealed class PevnyCas(DateTimeOffset ted) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ted;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class PevneProdleni(ProdleniPrehled prehled) : IProdleniService
    {
        public int Volani { get; private set; }

        public Task<ProdleniPrehled> GetPrehledAsync(int projektId, CancellationToken ct)
        {
            Volani++;
            return Task.FromResult(prehled);
        }

        public Task<ProdleniPrehled> GetPrehledProObdobiAsync(int projektId, DateTime obdobiOd, CancellationToken ct)
        {
            Volani++;
            return Task.FromResult(prehled);
        }
    }
}
