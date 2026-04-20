using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Request-scoped cache pro HOT lookupy. V rámci jednoho requestu (jednoho scope)
/// ručí, že se stejný ticket / kalkulace načte nejvýše jednou z DB. Scope = 1 HTTP request.
/// </summary>
public sealed class CachingTicketingQueryService : ITicketingQueryService
{
    private readonly ITicketingQueryService _inner;
    private readonly Dictionary<string, HotZaznamDto?> _zaznamyCache = new();
    private readonly Dictionary<string, HotKalkulaceDto?> _kalkulaceCache = new();

    public CachingTicketingQueryService(ITicketingQueryService inner)
    {
        _inner = inner;
    }

    public async Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        if (_zaznamyCache.TryGetValue(cislo, out var cached)) return cached;
        var result = await _inner.GetZaznamAsync(cislo, ct);
        _zaznamyCache[cislo] = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        var missing = cisla.Where(c => !_zaznamyCache.ContainsKey(c)).ToArray();
        if (missing.Length > 0)
        {
            var fetched = await _inner.GetZaznamyAsync(missing, ct);
            foreach (var c in missing)
                _zaznamyCache[c] = fetched.GetValueOrDefault(c);
        }

        var result = new Dictionary<string, HotZaznamDto>(cisla.Count);
        foreach (var c in cisla)
        {
            if (_zaznamyCache.TryGetValue(c, out var dto) && dto != null)
                result[c] = dto;
        }
        return result;
    }

    public async Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        if (_kalkulaceCache.TryGetValue(cislo, out var cached)) return cached;
        var result = await _inner.GetAkceptovanouKalkulaciAsync(cislo, ct);
        _kalkulaceCache[cislo] = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        var missing = cisla.Where(c => !_kalkulaceCache.ContainsKey(c)).ToArray();
        if (missing.Length > 0)
        {
            var fetched = await _inner.GetAkceptovaneKalkulaceAsync(missing, ct);
            foreach (var c in missing)
                _kalkulaceCache[c] = fetched.GetValueOrDefault(c);
        }

        var result = new Dictionary<string, HotKalkulaceDto>(cisla.Count);
        foreach (var c in cisla)
        {
            if (_kalkulaceCache.TryGetValue(c, out var dto) && dto != null)
                result[c] = dto;
        }
        return result;
    }
}
