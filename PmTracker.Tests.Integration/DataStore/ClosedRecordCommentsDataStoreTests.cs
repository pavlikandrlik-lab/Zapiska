using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Záznam ve stavu is_final nepřijímá nová vyjádření (2026-10-14). Pozastavené (is_final = 0)
/// se dál dopisují. Úpravy a mazání vyjádření u uzavřeného jednání zůstávají zakázané.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ClosedRecordCommentsDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ClosedRecordCommentsDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int OwnerId, int SubsystemId, int OpenRecordId, int ClosedRecordId, int MeetingId, CurrentUserContextViewModel Pm);

    private static async Task<Scenario> SeedAsync(PmTracker.Web.Data.PmTrackerDbContext dbContext, string marker, int meetingNumber)
    {
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Owner");
        var pmId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Pm");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker.ToUpperInvariant());
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}S", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        var openRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", $"{marker}Open");
        var closedRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", $"{marker}Closed");
        await SetRecordStateAsync(dbContext, closedRecordId, isFinal: true);
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber);

        var pm = IntegrationTestHelper.BuildUser(
            pmId,
            isSuperAdmin: false,
            grants: new[]
            {
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.CommentsAdd, projectId),
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.MeetingsNotesEdit, projectId)
            });
        return new Scenario(projectId, ownerId, subsystemId, openRecordId, closedRecordId, meetingId, pm);
    }

    private static async Task SetRecordStateAsync(PmTracker.Web.Data.PmTrackerDbContext dbContext, int recordId, bool isFinal)
    {
        var stateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal == isFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
        record.StavUkoluId = stateId;
        await dbContext.SaveChangesAsync();
    }

    /// <summary>Číslo, které uživatel vidí na kartě — hláška se na něj odkazuje, ne na interní id.</summary>
    private static async Task<string> VisibleNumberAsync(PmTracker.Web.Data.PmTrackerDbContext dbContext, int recordId)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().FirstAsync(x => x.Id == recordId);
        return string.IsNullOrWhiteSpace(record.CisloViditelne)
            ? record.CisloZaznamu.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : record.CisloViditelne.Trim();
    }

    [Fact]
    public async Task AddComment_OpenRecord_Passes()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_open_ok");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsOk", 9601);

        store.AddComment(new AddCommentCommand { ZaznamId = s.OpenRecordId, JednaniId = s.MeetingId, Text = "Do otevřeného." }, s.Pm);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.ZaznamId == s.OpenRecordId)).Should().BeTrue();
    }

    [Fact]
    public async Task AddComment_ClosedRecord_IsRejectedWithClearMessage()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_rejected");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsRej", 9611);
        var visibleNumber = await VisibleNumberAsync(dbContext, s.ClosedRecordId);

        var act = () => store.AddComment(new AddCommentCommand { ZaznamId = s.ClosedRecordId, JednaniId = s.MeetingId, Text = "Do uzavřeného." }, s.Pm);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Záznam {visibleNumber} je uzavřený*vyjádření nelze přidat*znovu otevřete*");
        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.ZaznamId == s.ClosedRecordId)).Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_ReopenedRecord_Passes()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_reopen");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsRe", 9621);
        await SetRecordStateAsync(dbContext, s.ClosedRecordId, isFinal: false);

        store.AddComment(new AddCommentCommand { ZaznamId = s.ClosedRecordId, JednaniId = s.MeetingId, Text = "Po znovuotevření." }, s.Pm);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.ZaznamId == s.ClosedRecordId)).Should().BeTrue();
    }

    [Fact]
    public async Task AddComment_ClosedRecord_UserWithoutPermissionGetsPermissionError()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_noperm");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsNp", 9631);
        var outsiderId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ClsNpOutsider");
        var outsider = IntegrationTestHelper.BuildUser(outsiderId, isSuperAdmin: false, grants: Array.Empty<PermissionGrantViewModel>());

        var act = () => store.AddComment(new AddCommentCommand { ZaznamId = s.ClosedRecordId, JednaniId = s.MeetingId, Text = "Cizí." }, outsider);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Nemáte oprávnění*");
    }

    [Fact]
    public async Task SaveMeetingNotesBatch_ClosedRecordAmongOpen_SavesNothing()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_batch");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsBt", 9641);
        var visibleNumber = await VisibleNumberAsync(dbContext, s.ClosedRecordId);

        var act = () => store.SaveMeetingNotesBatch(
            s.MeetingId,
            new[] { (s.OpenRecordId, "Poznámka A"), (s.ClosedRecordId, "Poznámka B") },
            s.Pm);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Záznam {visibleNumber} je uzavřený*poznámku nelze zapsat*");
        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.JednaniId == s.MeetingId))
            .Should().BeFalse("dávka je all-or-nothing");
    }

    [Fact]
    public async Task UpdateAndDeleteComment_InClosedMeeting_StayForbidden()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_meeting_lock");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsMl", 9651);

        store.AddComment(new AddCommentCommand { ZaznamId = s.OpenRecordId, JednaniId = s.MeetingId, Text = "Před uzavřením jednání." }, s.Pm);
        var comment = await dbContext.Vyjadreni.AsNoTracking().FirstAsync(x => x.ZaznamId == s.OpenRecordId);

        var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == s.MeetingId);
        meeting.StavJednaniId = await dbContext.CiselnikStavuJednani.Where(x => x.Kod == "CLOSED").Select(x => x.Id).FirstAsync();
        await dbContext.SaveChangesAsync();

        var update = () => store.UpdateComment(new UpdateCommentCommand { Id = comment.Id, Text = "Přepis historie." }, s.Pm);
        var delete = () => store.DeleteComment(new DeleteCommentCommand { Id = comment.Id }, s.Pm);

        update.Should().Throw<InvalidOperationException>().WithMessage("*uzavřeného jednání*nelze upravovat ani mazat*");
        delete.Should().Throw<InvalidOperationException>().WithMessage("*uzavřeného jednání*nelze upravovat ani mazat*");
        (await dbContext.Vyjadreni.AsNoTracking().FirstAsync(x => x.Id == comment.Id)).TextVyjadreni
            .Should().Contain("Před uzavřením jednání.");
    }
}
