using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Dashboard;

namespace PmTracker.Web.Services;

public sealed partial class RecordService : IRecordService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IRichTextContentService richTextContentService;
    private readonly ICommentService commentService;
    private readonly IProjectService projectService;
    private readonly IRecordEditorQueriesComposition recordEditorQueriesComposition;
    private readonly IRecordWriteCommandsComposition recordWriteCommandsComposition;
    private readonly IPendingScheduleProposalLockEvaluator pendingScheduleProposalLockEvaluator;
    private readonly IPriorityMatrixRebuildService priorityMatrixRebuildService;
    private readonly IAuditWriteService auditWriteService;
    private readonly TimeProvider timeProvider;

    public RecordService(
        PmTrackerDbContext dbContext,
        IRichTextContentService richTextContentService,
        ICommentService commentService,
        IProjectService projectService,
        IRecordEditorQueriesComposition recordEditorQueriesComposition,
        IRecordWriteCommandsComposition recordWriteCommandsComposition,
        IPendingScheduleProposalLockEvaluator pendingScheduleProposalLockEvaluator,
        IPriorityMatrixRebuildService priorityMatrixRebuildService,
        IAuditWriteService auditWriteService,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.richTextContentService = richTextContentService;
        this.commentService = commentService;
        this.projectService = projectService;
        this.recordEditorQueriesComposition = recordEditorQueriesComposition;
        this.recordWriteCommandsComposition = recordWriteCommandsComposition;
        this.pendingScheduleProposalLockEvaluator = pendingScheduleProposalLockEvaluator;
        this.priorityMatrixRebuildService = priorityMatrixRebuildService;
        this.auditWriteService = auditWriteService;
        this.timeProvider = timeProvider;
    }

    public Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default)
        => projectService.ProjektExistsAsync(id, ct);

    public Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default)
        => projectService.BuildProjektDetailAsync(id, ct);

    public Task<ProjektZaznamCardShellViewModel?> BuildRecordCardShellAsync(int projectId, int recordId, CancellationToken ct = default)
        => projectService.BuildRecordCardShellAsync(projectId, recordId, ct);

    public Task<ZaznamCardDetailViewModel?> BuildRecordCardDetailAsync(int projectId, int recordId, CancellationToken ct = default)
        => projectService.BuildRecordCardDetailAsync(projectId, recordId, ct);

    public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, CancellationToken ct = default)
        => projectService.BuildRecordCommentsPanelAsync(projectId, recordId, ct);

    public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, int? limit, bool loadAll, CancellationToken ct = default)
        => projectService.BuildRecordCommentsPanelAsync(projectId, recordId, limit, loadAll, ct);

    public Task<ZaznamEditViewModel> BuildZaznamEditAsync(int id, CancellationToken ct = default)
        => BuildZaznamEditAsync(id, recordEditorQueriesComposition, ct);

    public Task<ZaznamEditViewModel> BuildZaznamCreateAsync(int projektId, int? jednaniId = null, CancellationToken ct = default)
        => BuildZaznamCreateAsync(projektId, jednaniId, recordEditorQueriesComposition, ct);

    public Task<int> SaveRecordAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => SaveRecordAsync(command, currentUser, recordWriteCommandsComposition, ct);

    public Task AssignMeetingIdentifierAsync(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => AssignMeetingIdentifierAsync(command, currentUser, recordWriteCommandsComposition, ct);

    public Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => commentService.AddCommentAsync(command, currentUser, ct);

    public Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => commentService.UpdateCommentAsync(command, currentUser, ct);

    public Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => commentService.DeleteCommentAsync(command, currentUser, ct);
}
