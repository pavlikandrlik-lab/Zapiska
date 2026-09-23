using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Middleware;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// 2026-09-08: tisk výzvy vracel HTTP 500 a v aplikaci po něm nezbyla žádná stopa —
/// nebylo poznat proč ani kde spadl. Vestavěný ExceptionHandler sice výjimku loguje,
/// ale bez cesty požadavku, takže z logu nejde určit, který endpoint selhal.
///
/// Tento middleware doplňuje kontext (metoda, cesta, dotaz, trace id, uživatel)
/// a výjimku pouští dál, aby chybovou stránku dál renderoval ExceptionHandler.
/// </summary>
public sealed class RequestExceptionLoggingMiddlewareTests
{
    private sealed record Zaznam(LogLevel Level, string Message, Exception? Exception);

    private sealed class ZachytavaciLogger : ILogger<RequestExceptionLoggingMiddleware>
    {
        public List<Zaznam> Zaznamy { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Zaznamy.Add(new Zaznam(logLevel, formatter(state, exception), exception));
    }

    private static DefaultHttpContext Kontext()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/Export/Vyzva/5/Word";
        ctx.Request.QueryString = new QueryString("?projektId=2");
        ctx.TraceIdentifier = "trace-abc";
        return ctx;
    }

    [Fact]
    public async Task Vyjimka_SeZalogujeSKontextemPozadavku()
    {
        var logger = new ZachytavaciLogger();
        var boom = new InvalidOperationException("boom");
        var middleware = new RequestExceptionLoggingMiddleware(_ => throw boom, logger);

        var act = async () => await middleware.InvokeAsync(Kontext());

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("boom");

        var zaznam = logger.Zaznamy.Should().ContainSingle().Which;
        zaznam.Level.Should().Be(LogLevel.Error);
        zaznam.Exception.Should().BeSameAs(boom, "log musí nést i stack trace, ne jen zprávu");
        zaznam.Message.Should().Contain("GET", "z logu musí být poznat, co uživatel volal");
        zaznam.Message.Should().Contain("/Export/Vyzva/5/Word", "bez cesty nejde určit endpoint");
        zaznam.Message.Should().Contain("projektId=2", "parametry rozhodují, se kterými daty to spadlo");
        zaznam.Message.Should().Contain("trace-abc", "trace id spojí log s hláškou na chybové stránce");
    }

    /// <summary>
    /// Výjimka se nesmí spolknout — chybovou stránku renderuje až UseExceptionHandler
    /// nad tímto middlewarem. Spolknutí by uživateli vrátilo prázdnou odpověď.
    /// </summary>
    [Fact]
    public async Task Vyjimka_SePustiDal()
    {
        var logger = new ZachytavaciLogger();
        var middleware = new RequestExceptionLoggingMiddleware(
            _ => throw new InvalidOperationException("boom"), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(Kontext()));
    }

    [Fact]
    public async Task BezVyjimky_SeNicNeloguje()
    {
        var logger = new ZachytavaciLogger();
        var middleware = new RequestExceptionLoggingMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(Kontext());

        logger.Zaznamy.Should().BeEmpty("úspěšné požadavky nemají zaplavovat log");
    }
}
