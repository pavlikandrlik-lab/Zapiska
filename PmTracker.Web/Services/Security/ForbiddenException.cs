namespace PmTracker.Web.Services.Security;

/// <summary>
/// Výjimka pro autorizační odmítnutí ve service vrstvě. Global exception handler
/// ji mapuje na HTTP 403 Forbidden. Používá se z <see cref="IAuthorizationService.RequirePermissionAsync"/>.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public string PermissionKey { get; }
    public int? ProjektId { get; }
    public int? SubsystemId { get; }

    public ForbiddenException(string permissionKey, int? projektId = null, int? subsystemId = null)
        : base(BuildMessage(permissionKey, projektId, subsystemId))
    {
        PermissionKey = permissionKey;
        ProjektId = projektId;
        SubsystemId = subsystemId;
    }

    private static string BuildMessage(string key, int? projektId, int? subsystemId)
    {
        var context = (projektId, subsystemId) switch
        {
            (int p, _) => $" na projektu {p}",
            (_, int s) => $" na subsystému {s}",
            _ => string.Empty
        };
        return $"Permission '{key}' odmítnuto{context}.";
    }
}
