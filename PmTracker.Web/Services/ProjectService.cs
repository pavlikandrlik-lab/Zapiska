using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Dashboard;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService :
    IProjectService,
    IProjectDetailComposition,
    IRecordEditorQueriesComposition,
    IRecordWriteCommandsComposition
{
    private readonly PmTrackerDbContext dbContext;
    private readonly ITextNormalizer textNormalizer;
    private readonly IPersonIdentityMatcher personIdentityMatcher;
    private readonly ICommentAuthorizationPolicy commentAuthorizationPolicy;
    private readonly IHarmonogramService harmonogramService;
    private readonly IMeetingService meetingService;
    private readonly ICommentService commentService;
    private readonly IPendingScheduleProposalLockEvaluator pendingScheduleProposalLockEvaluator;
    private readonly IPriorityMatrixRebuildService priorityMatrixRebuildService;
    private readonly IAuditWriteService auditWriteService;
    private readonly IRecordScheduleActualSourceResolver scheduleActualSourceResolver;
    private readonly ILookupTableCache lookupCache;
    private readonly IProjectRoleCache projectRoleCache;
    private readonly TimeProvider timeProvider;
    // Plán 4 Feature C Task 6 UI — pro load BindingKandidati per záznam (dropdown alternativ).
    private readonly PmTracker.Web.Services.Schedules.IHarmonogramSkutecnostSyncService harmonogramSkutecnostSync;

    public ProjectService(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher,
        ICommentAuthorizationPolicy commentAuthorizationPolicy,
        IHarmonogramService harmonogramService,
        IMeetingService meetingService,
        ICommentService commentService,
        IPendingScheduleProposalLockEvaluator pendingScheduleProposalLockEvaluator,
        IPriorityMatrixRebuildService priorityMatrixRebuildService,
        IAuditWriteService auditWriteService,
        IRecordScheduleActualSourceResolver scheduleActualSourceResolver,
        ILookupTableCache lookupCache,
        IProjectRoleCache projectRoleCache,
        TimeProvider timeProvider,
        PmTracker.Web.Services.Schedules.IHarmonogramSkutecnostSyncService harmonogramSkutecnostSync)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
        this.personIdentityMatcher = personIdentityMatcher;
        this.commentAuthorizationPolicy = commentAuthorizationPolicy;
        this.harmonogramService = harmonogramService;
        this.meetingService = meetingService;
        this.commentService = commentService;
        this.pendingScheduleProposalLockEvaluator = pendingScheduleProposalLockEvaluator;
        this.priorityMatrixRebuildService = priorityMatrixRebuildService;
        this.auditWriteService = auditWriteService;
        this.scheduleActualSourceResolver = scheduleActualSourceResolver;
        this.lookupCache = lookupCache;
        this.projectRoleCache = projectRoleCache;
        this.timeProvider = timeProvider;
        this.harmonogramSkutecnostSync = harmonogramSkutecnostSync;
    }
}
