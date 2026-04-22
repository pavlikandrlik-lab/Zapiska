namespace PmTracker.Web.Services.Security;

/// <summary>
/// Kanonická fasáda pro autorizační rozhodování. Jediný entry point pro:
/// výstavbu per-request <see cref="AuthorizationSnapshot"/>, kontrolu oprávnění
/// (<see cref="HasPermissionAsync"/>) a vynucení (<see cref="RequirePermissionAsync"/>).
/// </summary>
/// <remarks>
/// Všechny autorizační kontroly v aplikaci musí jít přes tento interface nebo přes
/// ASP.NET Core <c>[Authorize(Policy="permission:xxx")]</c> (který interně volá totéž).
/// Fáze D plánu <c>2026-04-22-authz-phases-c-to-f.md</c>.
/// </remarks>
public interface IAuthorizationService
{
    /// <summary>Postaví snapshot pro osobu (načte z DB). Cacheable per-request.</summary>
    Task<AuthorizationSnapshot> BuildSnapshotAsync(int osobaId, CancellationToken ct = default);

    /// <summary>Vrátí true, pokud osoba má oprávnění (s volitelným projekt/subsystem kontextem).</summary>
    Task<bool> HasPermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default);

    /// <summary>Vyhodí <see cref="ForbiddenException"/>, pokud osoba nemá oprávnění.</summary>
    Task RequirePermissionAsync(int osobaId, string permissionKey, int? projektId = null, int? subsystemId = null, CancellationToken ct = default);
}
