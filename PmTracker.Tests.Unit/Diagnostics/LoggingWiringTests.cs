using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// Piny na zapojení logování v Program.cs. Samotné zapojení nejde ověřit unit testem,
/// ale drží ho dva invarianty, které se snadno rozbijí přesunem řádku — a jejichž
/// porušení se pozná až tím, že v produkci zase nebude po chybě žádná stopa.
/// </summary>
public sealed class LoggingWiringTests
{
    private static string ProgramCs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        var root = dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
        return File.ReadAllText(Path.Combine(root, "PmTracker.Web/Program.cs"));
    }

    [Fact]
    public void SouborovyLog_JeZapnuty()
    {
        ProgramCs().Should().Contain("AddPmTrackerFileLog",
            "bez souborového cíle jde log jen do konzole, kterou IIS in-process zahazuje");
    }

    /// <summary>
    /// Logovací middleware musí být registrovaný AŽ ZA UseExceptionHandler, aby byl uvnitř
    /// něj: chytí výjimku první, zaloguje ji s kontextem a pustí dál na vykreslení stránky.
    /// Před handlerem by výjimku dostal až po zpracování a kontext by se ztratil.
    /// </summary>
    [Fact]
    public void LogovaciMiddleware_JeUvnitrExceptionHandleru()
    {
        var src = ProgramCs();

        var handler = src.IndexOf("UseExceptionHandler", StringComparison.Ordinal);
        var middleware = src.IndexOf("UseMiddleware<RequestExceptionLoggingMiddleware>", StringComparison.Ordinal);

        handler.Should().BeGreaterThan(-1, "chybová stránka se musí dál renderovat");
        middleware.Should().BeGreaterThan(handler,
            "registrace až za handlerem = middleware běží uvnitř něj a výjimku chytí první");
    }
}
