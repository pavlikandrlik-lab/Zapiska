using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService : IMeetingService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly ITextNormalizer textNormalizer;
    private readonly IPersonIdentityMatcher personIdentityMatcher;
    private readonly ICommentService commentService;
    private readonly IAuditWriteService auditWriteService;
    private readonly TimeProvider timeProvider;

    public MeetingService(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher,
        ICommentService commentService,
        IAuditWriteService auditWriteService,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
        this.personIdentityMatcher = personIdentityMatcher;
        this.commentService = commentService;
        this.auditWriteService = auditWriteService;
        this.timeProvider = timeProvider;
    }
}
