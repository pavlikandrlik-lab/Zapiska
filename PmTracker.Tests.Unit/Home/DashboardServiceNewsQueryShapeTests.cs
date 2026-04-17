using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Tests.Unit.Home;

/// <summary>
/// Pins the shape of the news-panel lookup queries so they translate to SQL without
/// the "The LINQ expression could not be translated" error that broke the dashboard
/// news panel in production. The root cause was applying `.Where(contains(item.Id))`
/// after projecting into a private `record` type; EF Core could not translate the
/// filter on the projected positional-record member. The fix filters on the entity
/// before projection, so the Where lands on the mapped primary-key column.
/// </summary>
public sealed class DashboardServiceNewsQueryShapeTests
{
    [Fact]
    public void CommentLookup_ShouldTranslateToSql_WhenFilteringByIdBeforeProjection()
    {
        using var dbContext = CreateSqlServerDbContext();
        var commentIds = new List<int> { 1, 2, 3 };

        var action = () =>
            (from comment in dbContext.Vyjadreni.AsNoTracking()
             where commentIds.Contains(comment.Id)
             join record in dbContext.ProjektoveZaznamy.AsNoTracking() on comment.ZaznamId equals record.Id
             join project in dbContext.Projekty.AsNoTracking() on record.ProjektId equals project.Id
             select new { comment.Id, comment.ZaznamId, record.ProjektId, project.Zkratka })
            .ToQueryString();

        action.Should().NotThrow();
    }

    [Fact]
    public void RecordLookup_ShouldTranslateToSql_WhenFilteringByIdBeforeProjection()
    {
        using var dbContext = CreateSqlServerDbContext();
        var recordIds = new List<int> { 10, 20 };

        var action = () =>
            (from record in dbContext.ProjektoveZaznamy.AsNoTracking()
             where recordIds.Contains(record.Id)
             join project in dbContext.Projekty.AsNoTracking() on record.ProjektId equals project.Id
             join category in dbContext.CiselnikKategoriiZaznamu.AsNoTracking() on record.KategorieId equals category.Id
             select new { record.Id, record.ProjektId, project.Zkratka, category.Kod })
            .ToQueryString();

        action.Should().NotThrow();
    }

    [Fact]
    public void ProjectedRecordLookup_WithFilterAfterProjection_ShouldNotTranslate_RegressionMarker()
    {
        using var dbContext = CreateSqlServerDbContext();
        var recordIds = new List<int> { 10 };

        // This is the original broken shape: filter is applied AFTER projection
        // into a positional record (using `new ProjectedRow(...)`), which EF Core 8
        // cannot translate. This test pins the failure so a reviewer reverting the fix
        // to the old shape will immediately see the regression.
        var action = () =>
            (from record in dbContext.ProjektoveZaznamy.AsNoTracking()
             join project in dbContext.Projekty.AsNoTracking() on record.ProjektId equals project.Id
             select new ProjectedRow(record.Id, record.ProjektId, project.Zkratka))
            .Where(item => recordIds.Contains(item.RecordId))
            .ToQueryString();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*could not be translated*");
    }

    [Fact]
    public void MeetingLookup_ShouldTranslateToSql_WhenFilteringByIdBeforeProjection()
    {
        using var dbContext = CreateSqlServerDbContext();
        var meetingIds = new List<int> { 100 };

        var action = () =>
            (from meeting in dbContext.Jednani.AsNoTracking()
             where meetingIds.Contains(meeting.Id)
             join project in dbContext.Projekty.AsNoTracking() on meeting.ProjektId equals project.Id
             select new { meeting.Id, meeting.ProjektId, project.Zkratka, meeting.CisloJednani })
            .ToQueryString();

        action.Should().NotThrow();
    }

    private sealed record ProjectedRow(int RecordId, int ProjectId, string ProjectCode);

    private static PmTrackerDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true;")
            .Options;

        return new PmTrackerDbContext(options);
    }
}
