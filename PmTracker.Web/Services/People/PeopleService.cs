using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.People;

public sealed partial class PeopleService : IPeopleService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly ITextNormalizer textNormalizer;
    private readonly IAuditWriteService auditWriteService;

    public PeopleService(PmTrackerDbContext dbContext, ITextNormalizer textNormalizer, IAuditWriteService auditWriteService)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
        this.auditWriteService = auditWriteService;
    }
}
