using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Unit.Data;

/// <summary>
/// Review M3 (2026-10-08): selhání dopočtu (např. timeout) nesmí shodit start aplikace —
/// jednorázová dávková úloha je jiná kategorie než chybějící předpoklad schématu. Dávka
/// se navíc stránkuje podle Id, ne OFFSET od začátku (jinak práce roste s druhou mocninou
/// počtu řádků).
/// </summary>
public sealed class RichTextSearchTextBackfillHostedServiceTests
{
    private sealed record Zaznam(LogLevel Level, string Message, Exception? Exception);

    private sealed class ZachytavaciLogger : ILogger<RichTextSearchTextBackfillHostedService>
    {
        public List<Zaznam> Zaznamy { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Zaznamy.Add(new Zaznam(logLevel, formatter(state, exception), exception));
    }

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("backfill-" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task StartAsync_KdyzResolvePmTrackerDbContextSelze_NespadneALogujeError()
    {
        // Žádný DbContext není registrovaný — GetRequiredService uvnitř StartAsync spadne
        // přesně jako reálný výpadek DB (timeout, nedostupný server) při startu aplikace.
        var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var logger = new ZachytavaciLogger();
        var sut = new RichTextSearchTextBackfillHostedService(scopeFactory, logger);

        await sut.StartAsync(CancellationToken.None);

        var zaznam = logger.Zaznamy.Should().ContainSingle(
            "selhání se musí zalogovat, ne spolknout beze stopy").Which;
        zaznam.Level.Should().Be(LogLevel.Error);
        zaznam.Exception.Should().NotBeNull("operátor potřebuje stack trace, ne jen hlášku");
    }

    [Fact]
    public async Task BackfillAsync_DoplniStovkyChybejicich_PoDavkach_ANezacykliSeNaPrazdnem()
    {
        await using var db = NewDb();

        const int PocetStarych = 450;
        for (var i = 1; i <= PocetStarych; i++)
        {
            db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = i, Nazev = $"Záznam {i}", Popis = $"<p>text {i}</p>" });
        }

        // Pár řádků bez HTML — čistý text pro ně není co dopočítat, musí zůstat NULL.
        for (var i = PocetStarych + 1; i <= PocetStarych + 5; i++)
        {
            db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = i, Nazev = $"Bez popisu {i}" });
        }

        await db.SaveChangesAsync();

        // Simulace dat z doby před 1_4_7: čistý text se vynuluje, aniž by se změnilo HTML —
        // háček (RichTextSearchTextSync) ho nepřepočte, sleduje jen IsModified na Popis.
        foreach (var zaznam in await db.ProjektoveZaznamy.Where(z => z.Popis != null).ToListAsync())
        {
            zaznam.PopisProstyText = null;
        }

        await db.SaveChangesAsync();

        var doplneno = await RichTextSearchTextBackfillHostedService.BackfillAsync(db, CancellationToken.None);

        doplneno.Should().Be(PocetStarych);
        (await db.ProjektoveZaznamy.Where(z => z.Popis != null).AnyAsync(z => z.PopisProstyText == null))
            .Should().BeFalse("HTML je, čistý text musí být dopočtený");
        (await db.ProjektoveZaznamy.Where(z => z.Popis == null).AllAsync(z => z.PopisProstyText == null))
            .Should().BeTrue("bez HTML není co dopočítat — čistý text zůstává NULL");
    }
}
