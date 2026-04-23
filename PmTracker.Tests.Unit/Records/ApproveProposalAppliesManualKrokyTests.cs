using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Plán D Task 5+6: integrace ApplyApprovedScheduleProposalAsync a
/// ApproveCreateRecord... s novým payloadem (ManualActualKroky, HarmonogramVazby).
/// Test pracuje nad InMemory DB — mockuje autorizační policy tak, aby povolila
/// rozhodnout o návrhu. RecordService.SaveRecordAsync se mockuje také, protože
/// má mnoho DB dependencies mimo Plán D scope.
/// </summary>
public sealed class ApproveProposalAppliesManualKrokyTests
{
    private static readonly Guid K2Key = Guid.Parse("22222222-2222-2222-2222-000000000002");
    private static readonly Guid K5Key = Guid.Parse("22222222-2222-2222-2222-000000000005");
    private static readonly Guid K8Key = Guid.Parse("22222222-2222-2222-2222-000000000008");
    private static readonly Guid K9Key = Guid.Parse("22222222-2222-2222-2222-000000000009");

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("planD-" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task SeedSchemaAsync(PmTrackerDbContext db, int sablonaVerze = 1)
    {
        // Duration rows pro kroky 1..10
        var nextId = 1;
        for (int p = 1; p <= 10; p++)
        {
            db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Id = nextId++,
                Kod = $"HS{p:D2}_DURATION",
                Nazev = $"Krok {p}",
                Hodnota = 10,
                IsLocked = false,
                SablonaVerze = sablonaVerze,
                KrokKey = p switch
                {
                    2 => K2Key,
                    5 => K5Key,
                    8 => K8Key,
                    9 => K9Key,
                    _ => Guid.NewGuid()
                },
                KrokPoradi = p,
                JeZpozdeni = false,
                BarvaHex = "#EF4444"
            });
        }
        // Delay rows pro stejné kroky — sdílí KrokKey
        for (int p = 1; p <= 10; p++)
        {
            db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Id = 100 + p,
                Kod = $"HS{p:D2}_DELAY",
                Nazev = $"Krok {p} delay",
                Hodnota = 0,
                IsLocked = false,
                SablonaVerze = sablonaVerze,
                KrokKey = p switch
                {
                    2 => K2Key,
                    5 => K5Key,
                    8 => K8Key,
                    9 => K9Key,
                    _ => db.CiselnikHarmonogramTypu.Local.First(t => t.KrokPoradi == p && !t.JeZpozdeni).KrokKey
                },
                KrokPoradi = p,
                JeZpozdeni = true,
                BarvaHex = "#DC2626"
            });
        }
        db.HarmonogramSablony.Add(new HarmonogramSablonaEntity
        {
            Verze = sablonaVerze,
            DelayBarvaHex = "#DC2626",
            IsAktivni = true,
            CreatedAt = new DateTime(2026, 1, 1)
        });
        await db.SaveChangesAsync();
    }

    private static RecordProposalService BuildSut(
        PmTrackerDbContext db,
        IRecordService? recordService = null,
        IHarvestScheduler? harvestScheduler = null,
        IRecordProposalAuthorizationPolicy? authPolicy = null)
    {
        var audit = new Mock<IAuditWriteService>();
        var priority = new Mock<IPriorityMatrixRebuildService>();
        var lockEvaluator = new Mock<IPendingScheduleProposalLockEvaluator>();
        lockEvaluator
            .Setup(x => x.EvaluateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingScheduleProposalLockState(false, null, null, false, false));

        var authMock = authPolicy ?? Mock.Of<IRecordProposalAuthorizationPolicy>(x =>
            x.CanDecideProjectProposalAsync(It.IsAny<int>(), It.IsAny<CurrentUserContextViewModel>(), It.IsAny<CancellationToken>())
                == Task.FromResult(true));

        var harmonogramService = NewHarmonogramService(db);

        return new RecordProposalService(
            dbContext: db,
            recordService: recordService ?? Mock.Of<IRecordService>(),
            authorizationPolicy: authMock,
            pendingScheduleProposalLockEvaluator: lockEvaluator.Object,
            payloadMapper: new RecordProposalPayloadMapper(),
            harmonogramService: harmonogramService,
            priorityMatrixRebuildService: priority.Object,
            auditWriteService: audit.Object,
            harvestScheduler: harvestScheduler ?? Mock.Of<IHarvestScheduler>(),
            timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task ApproveScheduleProposal_AppliesManualActualKroky_IntoDelayRows()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);

        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 10,
            ProjektId = 1,
            Nazev = "Z",
            DatumZalozeni = new DateTime(2026, 1, 1),
            DatumUkonceni = new DateTime(2026, 6, 1),
            HarmonogramSablonaVerze = 1,
            SubsystemId = 7
        });
        // Seed durations 10 dnů pro kroky 1..10 → plán kroku 2 končí 2026-01-20 (start 2026-01-01 + 2×10).
        for (int p = 1; p <= 10; p++)
        {
            db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = 10,
                TypId = p,
                HodnotaInt = 10,
                UpdatedAt = new DateTime(2026, 1, 1)
            });
        }
        await db.SaveChangesAsync();

        // Schedule návrh s ManualActualKroky: krok 5 skutečnost 2026-03-10
        // Plán konce kroku 5 (start + 5×10 dnů = 2026-02-20) → actual - plan = 18 dnů
        var payload = new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = 1,
                ZaznamId = 10,
                TerminUkonceni = new DateTime(2026, 6, 1),
                ChangesScheduleActual = true,
                ManualActualKroky =
                [
                    new ManualActualKrokDto
                    {
                        KrokKey = K5Key,
                        AbsolutniDatum = new DateOnly(2026, 3, 10)
                    }
                ]
            }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        db.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            Id = 77,
            ProjektId = 1,
            ZaznamId = 10,
            SubsystemId = 7,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = json,
            CreatedByOsobaId = 2,
            CreatedAt = new DateTime(2026, 4, 20)
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db);
        var result = await sut.ApproveProposalAsync(
            new ProposalDecisionCommand { ProjektId = 1, ProposalId = 77 },
            BuildUser(99));

        result.Should().Be(10);
        var delayRow = await db.ZaznamHarmonogramHodnoty.AsNoTracking()
            .FirstAsync(x => x.ZaznamId == 10 && x.TypId == 105);
        // krok 5 delay typ = 105 (100 + 5). Plán 2026-02-20, actual 2026-03-10 → odchylka = 18 dní.
        delayRow.HodnotaInt.Should().Be(18);

        var updated = await db.ZaznamNavrhy.AsNoTracking().FirstAsync(x => x.Id == 77);
        updated.Stav.Should().Be(RecordProposalStateCodes.Approved);
    }

    [Fact]
    public async Task ApproveScheduleProposal_ManualKrokOnNonManualStep_Throws()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);

        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 11,
            ProjektId = 1,
            Nazev = "Z",
            DatumZalozeni = new DateTime(2026, 1, 1),
            DatumUkonceni = new DateTime(2026, 6, 1),
            HarmonogramSablonaVerze = 1,
            SubsystemId = 7
        });
        for (int p = 1; p <= 10; p++)
        {
            db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = 11,
                TypId = p,
                HodnotaInt = 10,
                UpdatedAt = new DateTime(2026, 1, 1)
            });
        }

        // krok 3 není mezi ručními (je určen bublinami)
        var k3Key = db.CiselnikHarmonogramTypu.Local.First(t => t.KrokPoradi == 3 && !t.JeZpozdeni).KrokKey;
        var payload = new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = 1,
                ZaznamId = 11,
                TerminUkonceni = new DateTime(2026, 6, 1),
                ChangesScheduleActual = true,
                ManualActualKroky =
                [
                    new ManualActualKrokDto { KrokKey = k3Key, AbsolutniDatum = new DateOnly(2026, 2, 1) }
                ]
            }
        };
        db.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            Id = 78,
            ProjektId = 1,
            ZaznamId = 11,
            SubsystemId = 7,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
            CreatedByOsobaId = 2,
            CreatedAt = new DateTime(2026, 4, 20)
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db);
        var act = async () => await sut.ApproveProposalAsync(
            new ProposalDecisionCommand { ProjektId = 1, ProposalId = 78 },
            BuildUser(99));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*není mezi kroky*");
    }

    [Fact]
    public async Task ApproveCreateProposal_AppliesHarmonogramVazbyAndSchedulesHarvest()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);

        // Simulace: RecordService.SaveRecordAsync vytvoří záznam + externí odkaz a vrátí id=200.
        const int newRecordId = 200;
        var recordServiceMock = new Mock<IRecordService>();
        recordServiceMock
            .Setup(x => x.SaveRecordAsync(It.IsAny<SaveRecordCommand>(), It.IsAny<CurrentUserContextViewModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
                {
                    Id = newRecordId,
                    ProjektId = 1,
                    Nazev = "Nový",
                    DatumZalozeni = new DateTime(2026, 1, 1),
                    DatumUkonceni = new DateTime(2026, 6, 1),
                    HarmonogramSablonaVerze = 1,
                    SubsystemId = 7
                });
                db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 501, ZaznamId = newRecordId, Cislo = "A1" });
                db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 502, ZaznamId = newRecordId, Cislo = "A2" });
                db.SaveChanges();
                return newRecordId;
            });

        var harvestSchedulerMock = new Mock<IHarvestScheduler>();
        harvestSchedulerMock
            .Setup(x => x.ScheduleHarvestForRecordAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SdReactiveSource>()))
            .Returns(Task.CompletedTask);

        var k3Key = db.CiselnikHarmonogramTypu.Local.First(t => t.KrokPoradi == 3 && !t.JeZpozdeni).KrokKey;
        var k6Key = db.CiselnikHarmonogramTypu.Local.First(t => t.KrokPoradi == 6 && !t.JeZpozdeni).KrokKey;
        var payload = new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.CreateRecord,
            CreateRecord = new CreateRecordProposalPayload
            {
                ProjektId = 1,
                Kategorie = "Úkol",
                Stav = "Nový",
                Nazev = "N1",
                VlastnikId = 10,
                Subsystem = "SYS",
                DatumZalozeni = new DateTime(2026, 1, 1),
                TerminUkonceni = new DateTime(2026, 6, 1),
                ExterniVazby =
                [
                    new SaveRecordExterniVazbaCommand { Typ = "OBJ", Cislo = "A1" },
                    new SaveRecordExterniVazbaCommand { Typ = "OBJ", Cislo = "A2" }
                ],
                HarmonogramVazby =
                [
                    new HarmonogramVazbaDto
                    {
                        KrokKey = k3Key,
                        ExterniOdkazIndex = 0,
                        HotVyjadreniId = 1001,
                        DatumVyjadreni = new DateTimeOffset(2026, 2, 5, 12, 0, 0, TimeSpan.Zero)
                    },
                    new HarmonogramVazbaDto
                    {
                        KrokKey = k6Key,
                        ExterniOdkazIndex = 1,
                        HotVyjadreniId = 1002,
                        DatumVyjadreni = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero)
                    }
                ]
            }
        };
        db.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            Id = 88,
            ProjektId = 1,
            ZaznamId = null,
            SubsystemId = 7,
            TypNavrhu = RecordProposalTypeCodes.CreateRecord,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
            CreatedByOsobaId = 2,
            CreatedAt = new DateTime(2026, 4, 20)
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db, recordServiceMock.Object, harvestSchedulerMock.Object);
        var result = await sut.ApproveProposalAsync(
            new ProposalDecisionCommand { ProjektId = 1, ProposalId = 88 },
            BuildUser(99));

        result.Should().Be(newRecordId);
        var vazby = await db.VyjadreniVazby.AsNoTracking().Where(x => x.ZaznamId == newRecordId).ToListAsync();
        vazby.Should().HaveCount(2);
        vazby.Should().Contain(v => v.KrokKey == k3Key && v.ExterniOdkazId == 501 && v.HotVyjadreniId == 1001 && v.Source == (byte)VazbaSource.Manual);
        vazby.Should().Contain(v => v.KrokKey == k6Key && v.ExterniOdkazId == 502 && v.HotVyjadreniId == 1002 && v.Source == (byte)VazbaSource.Manual);

        harvestSchedulerMock.Verify(x => x.ScheduleHarvestForRecordAsync(newRecordId, It.IsAny<CancellationToken>(), It.IsAny<SdReactiveSource>()), Times.Once);
    }

    private static IHarmonogramService NewHarmonogramService(PmTrackerDbContext db)
    {
        var audit = new Mock<IAuditWriteService>().Object;
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 10, 0, 0, TimeSpan.Zero));
        return new HarmonogramService(db, audit, time, NullLogger<HarmonogramService>.Instance);
    }

    private static CurrentUserContextViewModel BuildUser(int osobaId) => new()
    {
        OsobaId = osobaId,
        Jmeno = "Test",
        Prijmeni = "User",
        DisplayName = "Test User",
        Email = "test@example.com",
        OrganizacniCelek = "OC",
        RoleKody = Array.Empty<string>(),
        VisibleProjectIds = Array.Empty<int>(),
        DeletedProjectIds = Array.Empty<int>()
    };

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
