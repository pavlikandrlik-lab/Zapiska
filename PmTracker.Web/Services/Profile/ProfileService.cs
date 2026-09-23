using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Profile;

public sealed partial class ProfileService : IProfileService
{
    private readonly PmTrackerDbContext dbContext;

    // A5 (2026-07-08): IUserAuthorizationAuditSnapshotBuilder odstraněn — profil skládá
    // role instance přímými dotazy (PageQueries), granty audit snapshotu už nekonzumuje.
    public ProfileService(PmTrackerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }
}
