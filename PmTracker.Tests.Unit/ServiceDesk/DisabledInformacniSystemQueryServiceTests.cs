using FluentAssertions;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class DisabledInformacniSystemQueryServiceTests
{
    private static readonly DisabledInformacniSystemQueryService _svc = new();

    [Fact]
    public async Task GetAktivniIsAsync_VraciPrazdny()
    {
        var r = await _svc.GetAktivniIsAsync(CancellationToken.None);
        r.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProdleneAsync_VraciPrazdny()
    {
        var r = await _svc.GetProdleneAsync(1, DateTime.UtcNow, CancellationToken.None);
        r.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRozpocetAsync_VraciNull()
    {
        var r = await _svc.GetRozpocetAsync(1, CancellationToken.None);
        r.Should().BeNull();
    }
}
