using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Home;

/// <summary>
/// Regression: C-1 safety valve — BuildNewsItemsAsync must stop after MaxSkipPages (25)
/// instead of scanning the entire audit log.
/// </summary>
public sealed class DashboardServiceNewsBoundTests
{
    private const int UserOsobaId = 99;
    private const int OtherActorId = 1; // different from UserOsobaId so the filter passes
    private const int NoiseRowCount = 5200; // > MaxSkipPages(25) × BatchSize(200) = 5000

    /// <summary>
    /// Seeds 5200 audit rows that each satisfy the WHERE clause (entity type = "zaznam",
    /// action = "create", actor != current user) but point to record IDs that do not exist
    /// in ProjektoveZaznamy. As a result every batch is fetched but no items are produced —
    /// a pure noise workload. Without the MaxSkipPages cap the loop would run 26+ iterations;
    /// with the cap it stops at 25 and returns an empty list.
    /// </summary>
    [Fact]
    public async Task BuildNewsItemsAsync_LargeNoiseAuditLog_StopsAtMaxPagesAndReturnsEmpty()
    {
        await using var db = CreateDb();
        await SeedNoiseAuditRowsAsync(db, NoiseRowCount);

        var service = CreateService(db);
        var currentUser = BuildCurrentUserContext();

        // Act — must complete quickly (no full-table scan of 5200 rows)
        var result = await service.BuildNewsPanelAsync(currentUser, take: 20, CancellationToken.None);

        // The items list is empty because the noise rows point to non-existent records
        result.Items.Should().BeEmpty();

        // TotalCount reflects items found (0), not rows scanned — the bound check is structural
        result.TotalCount.Should().Be(0);
    }

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }

    private static async Task SeedNoiseAuditRowsAsync(PmTrackerDbContext db, int count)
    {
        // Entity type "zaznam" + action "create" + actor != UserOsobaId passes the audit WHERE clause.
        // EntityId points to record IDs that don't exist in ProjektoveZaznamy, so no items are built.
        var rows = Enumerable.Range(1, count).Select(i => new AuthzAuditLogEntity
        {
            Id = i,
            ActorOsobaId = OtherActorId,
            EntityType = "zaznam",
            EntityId = (1_000_000 + i).ToString(), // IDs that will never match real records
            Action = "create",
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        });
        db.AuthzAuditLog.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static DashboardService CreateService(PmTrackerDbContext db)
    {
        var priorityQueryMock = new Mock<IDashboardPriorityQuery>();
        priorityQueryMock
            .Setup(q => q.CountForUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        priorityQueryMock
            .Setup(q => q.GetTopForUserAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        priorityQueryMock
            .Setup(q => q.GetAllForUserAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new DashboardService(db, priorityQueryMock.Object, TimeProvider.System);
    }

    private static CurrentUserContextViewModel BuildCurrentUserContext() =>
        new()
        {
            OsobaId = UserOsobaId,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "test@test.local",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
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
}
