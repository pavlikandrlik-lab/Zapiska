using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class CommentCreateAuthorizationDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public CommentCreateAuthorizationDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddComment_ShouldAllowSubsystemDeputyLead_ForAssignedSubsystemOnly()
    {
        var db = await _fixture.CreateDatabaseAsync("comment_add_deputy_scope");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var deputyId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DeputyScope");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DeputyScopeOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "CDEPUTY");
        var allowedSubsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "CDEPA", ownerId);
        var deniedSubsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "CDEPB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, allowedSubsystemId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, deniedSubsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, allowedSubsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var allowedRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, allowedSubsystemId, "U", "DeputyAllowed");
        var deniedRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, deniedSubsystemId, "U", "DeputyDenied");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "DRAFT", meetingNumber: 9501);

        var deputyContext = store.BuildCurrentUserContext(deputyId.ToString(CultureInfo.InvariantCulture));
        deputyContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId).Should().BeTrue();

        store.AddComment(new AddCommentCommand
        {
            ZaznamId = allowedRecordId,
            JednaniId = meetingId,
            Text = "Deputy lead can comment."
        }, deputyContext);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x =>
            x.ZaznamId == allowedRecordId &&
            x.JednaniId == meetingId &&
            x.AutorOsobaId == deputyId))
            .Should()
            .BeTrue();

        var deniedAction = () => store.AddComment(new AddCommentCommand
        {
            ZaznamId = deniedRecordId,
            JednaniId = meetingId,
            Text = "Deputy lead out of scope."
        }, deputyContext);

        deniedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění přidat vyjádření*");
    }

    [Fact]
    public async Task AddComment_ShouldAllowSubsystemLead_ForAssignedSubsystemOnly()
    {
        var db = await _fixture.CreateDatabaseAsync("comment_add_lead_scope");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var leadId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LeadScope");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LeadScopeOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "CLEAD");
        var allowedSubsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "CLEADA", ownerId);
        var deniedSubsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "CLEADB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, allowedSubsystemId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, deniedSubsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, allowedSubsystemId, leadId, SubsystemRoleCodes.Lead);
        var allowedRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, allowedSubsystemId, "U", "LeadAllowed");
        var deniedRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, deniedSubsystemId, "U", "LeadDenied");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "DRAFT", meetingNumber: 9502);

        var leadContext = store.BuildCurrentUserContext(leadId.ToString(CultureInfo.InvariantCulture));
        leadContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId).Should().BeTrue();

        store.AddComment(new AddCommentCommand
        {
            ZaznamId = allowedRecordId,
            JednaniId = meetingId,
            Text = "Subsystem lead can comment."
        }, leadContext);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x =>
            x.ZaznamId == allowedRecordId &&
            x.JednaniId == meetingId &&
            x.AutorOsobaId == leadId))
            .Should()
            .BeTrue();

        var deniedAction = () => store.AddComment(new AddCommentCommand
        {
            ZaznamId = deniedRecordId,
            JednaniId = meetingId,
            Text = "Subsystem lead out of scope."
        }, leadContext);

        deniedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění přidat vyjádření*");
    }

    [Fact]
    public async Task AddComment_ShouldAllowRecordOwner_WhenOwnerHasRecordsEditPermission()
    {
        var db = await _fixture.CreateDatabaseAsync("comment_add_owner_records_edit");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "OwnerRecordsEdit");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "COWNER");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "COWNA", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectAdmin);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "OwnerRecord");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9503);

        var ownerContext = store.BuildCurrentUserContext(ownerId.ToString(CultureInfo.InvariantCulture));
        ownerContext.HasPermission(PermissionKeys.RecordsEdit, projectId).Should().BeTrue();

        store.AddComment(new AddCommentCommand
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            Text = "Owner comment with records.edit."
        }, ownerContext);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x =>
            x.ZaznamId == recordId &&
            x.JednaniId == meetingId &&
            x.AutorOsobaId == ownerId))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task AddComment_ShouldDenySubsystemLeadSpecialPermission_InOpenMeeting()
    {
        var db = await _fixture.CreateDatabaseAsync("comment_add_lead_open_denied");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var leadId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LeadOpenDenied");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LeadOpenDeniedOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "COPENLEAD");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "COPENLEADSUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, leadId, SubsystemRoleCodes.Lead);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "LeadOpenDeniedRecord");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9504);

        var leadContext = store.BuildCurrentUserContext(leadId.ToString(CultureInfo.InvariantCulture));
        leadContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId).Should().BeTrue();
        leadContext.HasPermission(PermissionKeys.RecordsEdit, projectId).Should().BeFalse();

        var action = () => store.AddComment(new AddCommentCommand
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            Text = "Subsystem lead cannot comment in OPEN without records.edit."
        }, leadContext);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění přidat vyjádření*");
    }
}
