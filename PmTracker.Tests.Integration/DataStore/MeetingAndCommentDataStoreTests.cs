using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingAndCommentDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public MeetingAndCommentDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteMeeting_ShouldCascadeAttendanceAndComments_AndWriteAudit()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_delete");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DeleteOwner");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "DEL_SUB", ownerId);
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "DELPRJ");
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "DeleteMeeting");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9100);

        var attendanceStateId = await dbContext.CiselnikStavuUcasti.Select(x => x.Id).FirstAsync();
        dbContext.Ucast.Add(new UcastEntity
        {
            JednaniId = meetingId,
            OsobaId = ownerId,
            StavUcastiId = attendanceStateId
        });

        dbContext.Vyjadreni.Add(new VyjadreniEntity
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            AutorOsobaId = ownerId,
            TextVyjadreni = "Comment for deletion",
            DatumVyjadreni = DateTime.Now
        });

        await dbContext.SaveChangesAsync();

        var currentUser = IntegrationTestHelper.BuildUser(ownerId, isSuperAdmin: true);
        store.DeleteMeeting(new DeleteMeetingCommand
        {
            JednaniId = meetingId,
            ProjektId = projectId
        }, currentUser);

        (await dbContext.Jednani.AnyAsync(x => x.Id == meetingId)).Should().BeFalse();
        (await dbContext.Ucast.AnyAsync(x => x.JednaniId == meetingId)).Should().BeFalse();
        (await dbContext.Vyjadreni.AnyAsync(x => x.JednaniId == meetingId)).Should().BeFalse();

        var auditRow = await dbContext.AuthzAuditLog
            .AsNoTracking()
            .Where(x => x.EntityType == "jednani" && x.EntityId == meetingId.ToString() && x.Action == "delete")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        auditRow.Should().NotBeNull();
    }

    [Fact]
    public async Task SubsystemLeaderPermission_ShouldAllowOwnCommentCrud_AndBlockOthers()
    {
        var db = await _fixture.CreateDatabaseAsync("comment_permissions");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var leaderId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LeadComment");
        var outsiderId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OutComment");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "COMPRJ");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "COM_SUB", leaderId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, leaderId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, leaderId, SubsystemRoleCodes.Lead);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, leaderId, subsystemId, "U", "CommentPerm");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "DRAFT", meetingNumber: 9200);

        var leaderContext = IntegrationTestHelper.BuildUser(
            leaderId,
            isSuperAdmin: false,
            grants: new[] { IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId) });

        store.AddComment(new AddCommentCommand
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            Text = "Subsystem lead comment"
        }, leaderContext);

        var comment = await dbContext.Vyjadreni
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstAsync(x => x.ZaznamId == recordId && x.JednaniId == meetingId);

        comment.AutorOsobaId.Should().Be(leaderId);

        var outsiderContext = IntegrationTestHelper.BuildUser(
            outsiderId,
            isSuperAdmin: false,
            grants: new[] { IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId) });

        var outsiderUpdate = () => store.UpdateComment(new UpdateCommentCommand
        {
            Id = comment.Id,
            Text = "Outsider update attempt"
        }, outsiderContext);

        outsiderUpdate.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění*upravit*");

        store.UpdateComment(new UpdateCommentCommand
        {
            Id = comment.Id,
            Text = "Leader update"
        }, leaderContext);

        (await dbContext.Vyjadreni.AsNoTracking().FirstAsync(x => x.Id == comment.Id)).TextVyjadreni
            .Should()
            .Be("Leader update");

        var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == meetingId);
        meeting.UzamklOsobaId = leaderId;
        await dbContext.SaveChangesAsync();

        var deleteLocked = () => store.DeleteComment(new DeleteCommentCommand { Id = comment.Id }, leaderContext);
        deleteLocked.Should().Throw<InvalidOperationException>()
            .WithMessage("*uzavřeného jednání*nelze upravovat ani mazat*");

        meeting.UzamklOsobaId = null;
        meeting.StavJednaniId = await dbContext.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync();
        await dbContext.SaveChangesAsync();

        store.DeleteComment(new DeleteCommentCommand { Id = comment.Id }, leaderContext);
        (await dbContext.Vyjadreni.AnyAsync(x => x.Id == comment.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_AndUpdateComment_ShouldSanitizeHtml_AndRejectEmptyRichText()
    {
        var db = await _fixture.CreateDatabaseAsync("comment_rich_text_sanitize");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RichCommentOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RCHPRJ");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RCH_SUB", ownerId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, ownerId, SubsystemRoleCodes.Lead);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "RichComment");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "DRAFT", meetingNumber: 9400);

        var ownerContext = IntegrationTestHelper.BuildUser(
            ownerId,
            isSuperAdmin: false,
            grants: new[] { IntegrationTestHelper.AllowProjectPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId) });

        store.AddComment(new AddCommentCommand
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            Text = "<p><strong>Safe</strong><script>alert(1)</script><a href=\"javascript:alert(1)\">bad</a><a href=\"https://example.com\">ok</a></p>"
        }, ownerContext);

        var savedComment = await dbContext.Vyjadreni
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstAsync(x => x.ZaznamId == recordId && x.JednaniId == meetingId);

        savedComment.TextVyjadreni.Should().Contain("<strong>Safe</strong>");
        savedComment.TextVyjadreni.Should().Contain("https://example.com");
        savedComment.TextVyjadreni.Should().NotContain("<script");
        savedComment.TextVyjadreni.Should().NotContain("javascript:");

        var emptyUpdate = () => store.UpdateComment(new UpdateCommentCommand
        {
            Id = savedComment.Id,
            Text = "<p><br></p>"
        }, ownerContext);

        emptyUpdate.Should().Throw<InvalidOperationException>()
            .WithMessage("*nesmí být prázdné*");
    }
}
