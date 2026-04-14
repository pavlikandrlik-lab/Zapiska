using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.Dictionaries;

public sealed partial class DictionaryService : IDictionaryService, IDictionariesCommandsComposition
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IHarmonogramService harmonogramService;
    private readonly IAuditWriteService auditWriteService;

    public DictionaryService(PmTrackerDbContext dbContext, IHarmonogramService harmonogramService, IAuditWriteService auditWriteService)
    {
        this.dbContext = dbContext;
        this.harmonogramService = harmonogramService;
        this.auditWriteService = auditWriteService;
    }

    public Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default)
        => harmonogramService.SaveHarmonogramStepRowAsync(command, ct);

    public Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default)
        => harmonogramService.DeleteHarmonogramStepRowAsync(command, ct);
}
