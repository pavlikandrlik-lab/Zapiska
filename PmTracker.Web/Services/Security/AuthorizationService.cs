using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

public sealed class AuthorizationService(PmTrackerDbContext db) : IAuthorizationService
{
    public async Task<AuthorizationSnapshot> BuildSnapshotAsync(int osobaId, CancellationToken ct = default)
    {
        var builder = new AuthorizationSnapshotBuilder(db);
        return await builder.BuildAsync(osobaId, ct);
    }

    public async Task<bool> HasPermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default)
    {
        var snapshot = await BuildSnapshotAsync(osobaId, ct);
        return snapshot.HasPermission(permissionKey, projektId, subsystemId);
    }

    public async Task RequirePermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(osobaId, permissionKey, projektId, subsystemId, ct))
        {
            throw new ForbiddenException(permissionKey, projektId, subsystemId);
        }
    }
}
