using Microsoft.Extensions.Caching.Memory;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Abstrahuje způsob, jak z AD (nebo z jiného zdroje) získat zobrazované jméno
/// pro daný login. Záměrně se nepoužívá IActiveDirectoryService přímo — ten v době
/// implementace neexponuje login→jméno lookup. Implementace v Plánu C je
/// konzervativní fallback (vrací null); jakmile AD vrstva dostane
/// <c>ResolveDisplayNameByLoginAsync</c>, adaptér se přidá bez změny <see cref="AdLoginCache"/>.
/// </summary>
public interface IAdLoginResolver
{
    /// <summary>
    /// Vrátí zobrazované jméno (typicky AD <c>cn</c>) nebo <c>null</c>,
    /// pokud login v AD neexistuje nebo je AD nedostupné.
    /// </summary>
    Task<string?> ResolveDisplayNameAsync(string login, CancellationToken ct);
}

/// <summary>
/// Konzervativní fallback — vrací null pro všechny loginy.
/// Používá se, dokud AD vrstva nedostane ResolveDisplayNameByLoginAsync.
/// </summary>
internal sealed class NoOpAdLoginResolver : IAdLoginResolver
{
    public Task<string?> ResolveDisplayNameAsync(string login, CancellationToken ct)
        => Task.FromResult<string?>(null);
}

/// <summary>
/// In-memory cache pro překlad AD login → zobrazované jméno. TTL 6 hodin.
/// Používá se v chat modalu, aby se u každé bubliny místo loginu „jan.novak"
/// zobrazilo „Jan Novák". Nikdy se neukládá do DB.
/// </summary>
/// <remarks>
/// Při cache miss + nedostupném AD (nebo prázdném výsledku) se vrací
/// <c>"Neznámý (login)"</c>. Tento fallback se vrací i při výjimce, aby UI nikdy
/// nepadlo kvůli AD.
/// </remarks>
public sealed class AdLoginCache
{
    internal static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(6);

    private readonly IAdLoginResolver _resolver;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdLoginCache>? _logger;
    private readonly TimeSpan _ttl;

    public AdLoginCache(IAdLoginResolver resolver, IMemoryCache cache)
        : this(resolver, cache, logger: null, ttl: DefaultTtl)
    {
    }

    public AdLoginCache(
        IAdLoginResolver resolver,
        IMemoryCache cache,
        ILogger<AdLoginCache>? logger,
        TimeSpan ttl)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger;
        _ttl = ttl <= TimeSpan.Zero ? DefaultTtl : ttl;
    }

    /// <summary>
    /// Vrací zobrazované jméno nebo <c>"Neznámý (login)"</c> fallback.
    /// Prázdný/whitespace login vrací <c>"Neznámý"</c>.
    /// </summary>
    public async Task<string> ResolveDisplayNameAsync(string? login, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return "Neznámý";
        }

        var normalized = login.Trim();
        var cacheKey = BuildCacheKey(normalized);

        if (_cache.TryGetValue<string>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        string? resolved;
        try
        {
            resolved = await _resolver.ResolveDisplayNameAsync(normalized, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "AD lookup selhal pro login {Login}.", normalized);
            resolved = null;
        }

        var result = string.IsNullOrWhiteSpace(resolved)
            ? $"Neznámý ({normalized})"
            : resolved.Trim();

        _cache.Set(cacheKey, result, _ttl);
        return result;
    }

    /// <summary>
    /// Odstraní login z cache — volá se po AD sync, aby se obnovil překlad.
    /// </summary>
    public void Invalidate(string login)
    {
        if (string.IsNullOrWhiteSpace(login)) return;
        _cache.Remove(BuildCacheKey(login.Trim()));
    }

    private static string BuildCacheKey(string login)
        => "adlogincache:" + login.ToLowerInvariant();
}
