using System.Collections.Concurrent;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IAuthorizationSnapshotBuilder _builder;

    // Per-instance (= per-request díky Scoped DI) cache snapshotů.
    // Ukládáme Task<T>, nikoli T — souběžná volání se tak napojí na stejný probíhající build.
    private readonly ConcurrentDictionary<int, Task<AuthorizationSnapshot>> _cache = new();

    /// <summary>Produkční konstruktor — builder vytvoří nad předaným DbContextem.</summary>
    public AuthorizationService(PmTrackerDbContext db)
        : this(new AuthorizationSnapshotBuilder(db)) { }

    /// <summary>
    /// Testovací konstruktor — umožňuje injektovat mock/stub builderu bez DB.
    /// Přístupný z PmTracker.Tests.Unit díky InternalsVisibleTo.
    /// </summary>
    internal AuthorizationService(IAuthorizationSnapshotBuilder builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Vrátí snapshot pro daného osobaId. Pokud byl snapshot pro tuto osobu již v rámci
    /// aktuální instance postaven, vrátí cached výsledek bez dalšího DB volání.
    ///
    /// Poznámka k CancellationToken: první volající "vlastní" CancellationToken, který
    /// řídí underlying DB dotazy. Souběžná volání se stejným osobaId se napojí na tentýž
    /// Task a jejich vlastní CT nemá vliv na průběh buildu. Pro per-request Scoped service
    /// (kde všechna CT odpovídají HttpContext.RequestAborted) je toto chování bezpečné.
    /// </summary>
    public Task<AuthorizationSnapshot> BuildSnapshotAsync(int osobaId, CancellationToken ct = default)
        => _cache.GetOrAdd(osobaId, id => _builder.BuildAsync(id, ct));

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
