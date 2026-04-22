using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.Dictionaries;

public sealed partial class DictionaryService : IDictionaryService, IDictionariesCommandsComposition
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IHarmonogramCatalogService harmonogramService;
    private readonly IAuditWriteService auditWriteService;
    private readonly TimeProvider timeProvider;

    public DictionaryService(PmTrackerDbContext dbContext, IHarmonogramCatalogService harmonogramService, IAuditWriteService auditWriteService, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.harmonogramService = harmonogramService;
        this.auditWriteService = auditWriteService;
        this.timeProvider = timeProvider;
    }

    public Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default)
        => harmonogramService.SaveHarmonogramStepRowAsync(command, ct);

    public Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default)
        => harmonogramService.DeleteHarmonogramStepRowAsync(command, ct);
}
