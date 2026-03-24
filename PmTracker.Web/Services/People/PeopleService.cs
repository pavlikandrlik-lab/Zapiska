using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.People;

public sealed partial class PeopleService : IPeopleService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly ITextNormalizer textNormalizer;

    public PeopleService(PmTrackerDbContext dbContext, ITextNormalizer textNormalizer)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
    }
}
