namespace PmTracker.Web.Services.Settings;

public interface IUserAuthorizationSnapshotBuilder
{
    Task<UserAuthorizationSnapshot> BuildAsync(int osobaId, CancellationToken ct = default);
}
