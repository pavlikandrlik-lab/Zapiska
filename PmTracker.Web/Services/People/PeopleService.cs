using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.People;

public sealed partial class PeopleService : IPeopleService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly ITextNormalizer textNormalizer;
    private readonly IAuditWriteService auditWriteService;
    private readonly IReactiveSyncQueue<AdReactiveSyncRequest> adReactiveQueue;

    public PeopleService(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IAuditWriteService auditWriteService,
        IReactiveSyncQueue<AdReactiveSyncRequest> adReactiveQueue)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
        this.auditWriteService = auditWriteService;
        this.adReactiveQueue = adReactiveQueue;
    }
}
