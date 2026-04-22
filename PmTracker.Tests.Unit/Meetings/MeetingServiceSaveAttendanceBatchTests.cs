using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Meetings;

/// <summary>
/// Regression tests for SaveAttendanceBatchAsync N+1 fix.
/// InMemory provider is used because this is a unit-test project;
/// the production execution strategy wrapping is verified by the fact
/// that the method is wrapped (confirmed by code review) — what we assert
/// here is the observable outcome: all rows are persisted correctly.
/// </summary>
public sealed class MeetingServiceSaveAttendanceBatchTests
{
    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // InMemory does not support real transactions; suppress the warning so
            // the test can exercise the execution-strategy wrapper path.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new PmTrackerDbContext(opts);
    }

    private static MeetingService CreateService(PmTrackerDbContext db)
        => new(db, null!, null!, new FakeCommentService(), new FakeAuditWriteService(), TimeProvider.System);

    private static CurrentUserContextViewModel BuildCurrentUser() =>
        new()
        {
            OsobaId = 1,
            Jmeno = "Test",
            Prijmeni = "Tester",
            DisplayName = "Test Tester",
            Email = "test@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };

    private static async Task SeedPrerequisitesAsync(PmTrackerDbContext db, int meetingId)
    {
        db.Jednani.Add(new JednaniEntity
        {
            Id = meetingId,
            ProjektId = 1,
            CisloJednani = 1,
            DatumPlanovane = DateTime.Today,
            StavJednaniId = 1
        });
        db.CiselnikStavuUcasti.Add(new CiselnikStavuUcastiEntity
        {
            Id = 1,
            Kod = "PRESENT",
            Nazev = "Přítomen"
        });
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task SaveAttendanceBatchAsync_WithThreeNewRows_AllRowsArePersisted()
    {
        // Arrange
        await using var db = CreateDb();
        await SeedPrerequisitesAsync(db, meetingId: 42);
        var sut = CreateService(db);
        var currentUser = BuildCurrentUser();
        var rows = new (int OsobaId, string StavUcasti)[]
        {
            (101, "PRESENT"),
            (102, "PRESENT"),
            (103, "PRESENT")
        };

        // Act
        await sut.SaveAttendanceBatchAsync(42, rows, currentUser);

        // Assert — all three attendance rows must be in the database
        var persisted = await db.Ucast
            .Where(x => x.JednaniId == 42)
            .OrderBy(x => x.OsobaId)
            .ToListAsync();

        persisted.Should().HaveCount(3, "all three input rows must be persisted in a single batch");
        persisted.Select(x => x.OsobaId).Should().BeEquivalentTo([101, 102, 103]);
        persisted.Should().AllSatisfy(x => x.StavUcastiId.Should().Be(1));
    }

    [Fact]
    public async Task SaveAttendanceBatchAsync_WithMixOfNewAndExistingRows_UpdatesExistingAndInsertsNew()
    {
        // Arrange
        await using var db = CreateDb();
        await SeedPrerequisitesAsync(db, meetingId: 43);

        // Seed existing attendance row for person 201 with state 1
        db.Ucast.Add(new UcastEntity { JednaniId = 43, OsobaId = 201, StavUcastiId = 1 });
        db.CiselnikStavuUcasti.Add(new CiselnikStavuUcastiEntity { Id = 2, Kod = "ABSENT", Nazev = "Omluven" });
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var currentUser = BuildCurrentUser();
        var rows = new (int OsobaId, string StavUcasti)[]
        {
            (201, "ABSENT"),   // update existing
            (202, "PRESENT"),  // insert new
        };

        // Act
        await sut.SaveAttendanceBatchAsync(43, rows, currentUser);

        // Assert
        var persisted = await db.Ucast
            .Where(x => x.JednaniId == 43)
            .OrderBy(x => x.OsobaId)
            .ToListAsync();

        persisted.Should().HaveCount(2);
        persisted.First(x => x.OsobaId == 201).StavUcastiId.Should().Be(2, "existing row should be updated to ABSENT");
        persisted.First(x => x.OsobaId == 202).StavUcastiId.Should().Be(1, "new row should be inserted as PRESENT");
    }

    [Fact]
    public async Task SaveAttendanceBatchAsync_WithEmptyRows_DoesNothing()
    {
        // Arrange
        await using var db = CreateDb();
        await SeedPrerequisitesAsync(db, meetingId: 44);
        var sut = CreateService(db);
        var currentUser = BuildCurrentUser();

        // Act
        await sut.SaveAttendanceBatchAsync(44, [], currentUser);

        // Assert
        var count = await db.Ucast.CountAsync(x => x.JednaniId == 44);
        count.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // Fakes
    // ---------------------------------------------------------------

    private sealed class FakeCommentService : ICommentService
    {
        public Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveMeetingNoteAsync(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAuditWriteService : IAuditWriteService
    {
        public void Add(int? actorOsobaId, AuditWriteEntry entry) { }
        public Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    }
}
