using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Services.Profile;

public sealed partial class ProfileService : IProfileService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly IUserAuthorizationAuditSnapshotBuilder userAuthorizationSnapshotBuilder;

    public ProfileService(
        PmTrackerDbContext dbContext,
        IUserAuthorizationAuditSnapshotBuilder userAuthorizationSnapshotBuilder)
    {
        this.dbContext = dbContext;
        this.userAuthorizationSnapshotBuilder = userAuthorizationSnapshotBuilder;
    }
}
