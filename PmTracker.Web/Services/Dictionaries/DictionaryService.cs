using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Dictionaries;

public sealed partial class DictionaryService : IDictionaryService, IDictionariesCommandsComposition
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IHarmonogramService harmonogramService;

    public DictionaryService(PmTrackerDbContext dbContext, IHarmonogramService harmonogramService)
    {
        this.dbContext = dbContext;
        this.harmonogramService = harmonogramService;
    }

    public Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default)
        => harmonogramService.SaveHarmonogramStepRowAsync(command, ct);

    public Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default)
        => harmonogramService.DeleteHarmonogramStepRowAsync(command, ct);
}
