using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.Dictionaries;

public sealed partial class DictionaryService : IDictionaryService, IDictionariesCommandsComposition
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IAuditWriteService auditWriteService;
    private readonly TimeProvider timeProvider;

    public DictionaryService(PmTrackerDbContext dbContext, IAuditWriteService auditWriteService, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.auditWriteService = auditWriteService;
        this.timeProvider = timeProvider;
    }
}
