using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class AdLoginCacheTests
{
    private static AdLoginCache Build(Mock<IAdLoginResolver>? resolver = null, IMemoryCache? cache = null)
    {
        var r = resolver ?? new Mock<IAdLoginResolver>();
        var c = cache ?? new MemoryCache(new MemoryCacheOptions());
        return new AdLoginCache(r.Object, c);
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_FirstCall_HitsResolver()
    {
        var r = new Mock<IAdLoginResolver>();
        r.Setup(x => x.ResolveDisplayNameAsync("jan.novak", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jan Novák");
        var sut = Build(r);

        var name = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);

        name.Should().Be("Jan Novák");
        r.Verify(x => x.ResolveDisplayNameAsync("jan.novak", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_SecondCall_UsesCache()
    {
        var r = new Mock<IAdLoginResolver>();
        r.Setup(x => x.ResolveDisplayNameAsync("jan.novak", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jan Novák");
        var sut = Build(r);

        _ = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);
        _ = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);

        r.Verify(x => x.ResolveDisplayNameAsync("jan.novak", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_NullFromResolver_ReturnsFallback()
    {
        var r = new Mock<IAdLoginResolver>();
        r.Setup(x => x.ResolveDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var sut = Build(r);

        var name = await sut.ResolveDisplayNameAsync("xyz.login", CancellationToken.None);

        name.Should().Be("Neznámý (xyz.login)");
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_EmptyLogin_ReturnsNeznamy()
    {
        var sut = Build();
        (await sut.ResolveDisplayNameAsync("", CancellationToken.None)).Should().Be("Neznámý");
        (await sut.ResolveDisplayNameAsync("   ", CancellationToken.None)).Should().Be("Neznámý");
        (await sut.ResolveDisplayNameAsync(null, CancellationToken.None)).Should().Be("Neznámý");
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_ResolverThrows_ReturnsFallback_AndCachesFallback()
    {
        var r = new Mock<IAdLoginResolver>();
        r.Setup(x => x.ResolveDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AD down"));
        var sut = Build(r);

        var name = await sut.ResolveDisplayNameAsync("broken.login", CancellationToken.None);
        name.Should().Be("Neznámý (broken.login)");

        // Druhé volání nesmí znovu hodit — použije cached fallback
        _ = await sut.ResolveDisplayNameAsync("broken.login", CancellationToken.None);
        r.Verify(x => x.ResolveDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveDisplayNameAsync_CaseInsensitiveCacheKey()
    {
        var r = new Mock<IAdLoginResolver>();
        r.Setup(x => x.ResolveDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jan Novák");
        var sut = Build(r);

        _ = await sut.ResolveDisplayNameAsync("Jan.Novak", CancellationToken.None);
        _ = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);
        _ = await sut.ResolveDisplayNameAsync("JAN.NOVAK", CancellationToken.None);

        // Pouze jeden zásah — AdLoginCache normalizuje na lower case
        r.Verify(x => x.ResolveDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalidate_RemovesCachedEntry()
    {
        var r = new Mock<IAdLoginResolver>();
        r.SetupSequence(x => x.ResolveDisplayNameAsync("jan.novak", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jan Novák")
            .ReturnsAsync("Jan Novák v2");
        var sut = Build(r);

        _ = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);
        sut.Invalidate("jan.novak");
        var name = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);

        name.Should().Be("Jan Novák v2");
    }

    [Fact]
    public async Task NoOpAdLoginResolver_AlwaysReturnsNull()
    {
        var sut = Build(new Mock<IAdLoginResolver>());
        var name = await sut.ResolveDisplayNameAsync("someone", CancellationToken.None);
        name.Should().Be("Neznámý (someone)");
    }
}
