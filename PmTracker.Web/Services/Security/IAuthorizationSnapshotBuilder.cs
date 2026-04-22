namespace PmTracker.Web.Services.Security;

/// <summary>
/// Interní seam pro stavbu <see cref="AuthorizationSnapshot"/> z DB.
/// Produkční implementací je <see cref="AuthorizationSnapshotBuilder"/>; v testech lze injektovat mock.
/// </summary>
internal interface IAuthorizationSnapshotBuilder
{
    Task<AuthorizationSnapshot> BuildAsync(int osobaId, CancellationToken ct);
}
