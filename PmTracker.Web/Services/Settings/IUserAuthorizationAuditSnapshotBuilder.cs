namespace PmTracker.Web.Services.Settings;

public interface IUserAuthorizationAuditSnapshotBuilder
{
    Task<UserAuthorizationAuditSnapshot> BuildAsync(int osobaId, CancellationToken ct = default);
}
