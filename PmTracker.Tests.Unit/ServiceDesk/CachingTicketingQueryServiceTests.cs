using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class CachingTicketingQueryServiceTests
{
    private sealed class CountingInner : ITicketingQueryService
    {
        public int GetZaznamCalls { get; private set; }
        public int GetZaznamyBatchCalls { get; private set; }
        public int GetAkceptovanouCalls { get; private set; }
        public int GetAkceptovaneBatchCalls { get; private set; }

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
        {
            GetZaznamCalls++;
            return Task.FromResult<HotZaznamDto?>(new HotZaznamDto(cislo, "PNF", "nazev", "popis"));
        }

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
        {
            GetZaznamyBatchCalls++;
            IReadOnlyDictionary<string, HotZaznamDto> dict =
                cisla.ToDictionary(c => c, c => new HotZaznamDto(c, "PNF", "n", "p"));
            return Task.FromResult(dict);
        }

        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
        {
            GetAkceptovanouCalls++;
            return Task.FromResult<HotKalkulaceDto?>(null);
        }

        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
        {
            GetAkceptovaneBatchCalls++;
            return Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(
                new Dictionary<string, HotKalkulaceDto>());
        }
    }

    [Fact]
    public async Task Single_DruheVolani_NespoustiInner()
    {
        var inner = new CountingInner();
        var cache = new CachingTicketingQueryService(inner);

        await cache.GetZaznamAsync("A", default);
        await cache.GetZaznamAsync("A", default);

        inner.GetZaznamCalls.Should().Be(1);
    }

    [Fact]
    public async Task Batch_DruheVolaniStejnychId_NespoustiInner()
    {
        var inner = new CountingInner();
        var cache = new CachingTicketingQueryService(inner);

        await cache.GetZaznamyAsync(new[] { "A", "B" }, default);
        await cache.GetZaznamyAsync(new[] { "A", "B" }, default);

        inner.GetZaznamyBatchCalls.Should().Be(1);
    }

    [Fact]
    public async Task Batch_ProlozeneIds_PoslePouzeChybejici()
    {
        var inner = new CountingInner();
        var cache = new CachingTicketingQueryService(inner);

        await cache.GetZaznamyAsync(new[] { "A" }, default);
        await cache.GetZaznamyAsync(new[] { "A", "B" }, default);

        inner.GetZaznamyBatchCalls.Should().Be(2);
    }
}
