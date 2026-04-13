using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Records;

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
    private readonly TimeProvider timeProvider;

    public ProjectService(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher,
        ICommentAuthorizationPolicy commentAuthorizationPolicy,
        IHarmonogramService harmonogramService,
        IMeetingService meetingService,
        ICommentService commentService,
        IPendingScheduleProposalLockEvaluator pendingScheduleProposalLockEvaluator,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
        this.personIdentityMatcher = personIdentityMatcher;
        this.commentAuthorizationPolicy = commentAuthorizationPolicy;
        this.harmonogramService = harmonogramService;
        this.meetingService = meetingService;
        this.commentService = commentService;
        this.pendingScheduleProposalLockEvaluator = pendingScheduleProposalLockEvaluator;
        this.timeProvider = timeProvider;
    }
}
