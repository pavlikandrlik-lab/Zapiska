using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// No-op <see cref="IMemoryCache"/> pro integration testy. Důvod: sdílená
/// <c>PmTrackerWebAppFactory</c> (CollectionFixture) by jinak držela snapshoty číselníků
/// z předchozích testů i po mutacích DB (např. <c>EnsureSubsystemAsync</c>).
/// Každý test dostává čerstvý DB dotaz, cache tu nic nešetří — testcontainer je dost rychlý.
/// </summary>
internal sealed class NullMemoryCache : IMemoryCache
{
    public ICacheEntry CreateEntry(object key) => new NullEntry(key);

    public void Remove(object key) { }

    public bool TryGetValue(object key, out object? value)
    {
        value = null;
        return false;
    }

    public void Dispose() { }

    private sealed class NullEntry : ICacheEntry
    {
        public NullEntry(object key) { Key = key; }

        public object Key { get; }
        public object? Value { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public IList<IChangeToken> ExpirationTokens { get; } = new List<IChangeToken>();
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<PostEvictionCallbackRegistration>();
        public CacheItemPriority Priority { get; set; }
        public long? Size { get; set; }

        public void Dispose() { }
    }
}
