using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Security;

/// <summary>
/// M-4: Intranet gov app má defense-in-depth security headers — CSP, X-Frame-Options,
/// X-Content-Type-Options, Referrer-Policy. Test ověří, že headers middleware v
/// Program.cs je registrován a obsahuje všechny povinné headery.
///
/// Full integration testy (Tests.Api) vyžadují Testcontainers SQL fixture, proto
/// tento test inspektuje zdrojový kód Program.cs — shodný vzor jako
/// PolicyAttributeMigrationTests.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public void Program_Should_SetSecurityHeaders_InMiddleware()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Program.cs"));

        code.Should().Contain("\"X-Content-Type-Options\"",
            "X-Content-Type-Options: nosniff — brání MIME sniffingu");
        code.Should().Contain("\"nosniff\"",
            "hodnota nosniff pro X-Content-Type-Options");

        code.Should().Contain("\"X-Frame-Options\"",
            "X-Frame-Options: DENY — brání clickjacking přes <iframe>");
        code.Should().Contain("\"DENY\"",
            "hodnota DENY pro X-Frame-Options");

        code.Should().Contain("\"Referrer-Policy\"",
            "Referrer-Policy: same-origin — omezuje leak Referer hlavičky");
        code.Should().Contain("\"same-origin\"",
            "hodnota same-origin pro Referrer-Policy");

        code.Should().Contain("\"Content-Security-Policy\"",
            "CSP hlavička definující povolené zdroje");
        code.Should().Contain("default-src 'self'",
            "CSP default-src musí být restriktivní ('self')");
        code.Should().Contain("frame-ancestors 'none'",
            "CSP frame-ancestors 'none' — druhá vrstva proti clickjackingu");
        code.Should().Contain("form-action 'self'",
            "CSP form-action 'self' — forma nemůže POSTovat mimo origin");
        code.Should().Contain("base-uri 'self'",
            "CSP base-uri 'self' — brání přepsání base href");
    }

    [Fact]
    public void Program_Should_NotUse_UnsafeEval_InCspDirective()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Program.cs"));

        // Specifický pattern: CSP direktiva s 'unsafe-eval'.
        // Komentáře zmiňující název jsou OK; jen CSP pravidlo nesmí hodnotu obsahovat.
        code.Should().NotContain("\"script-src 'self' 'unsafe-eval",
            "CSP script-src nesmí obsahovat 'unsafe-eval' — zbytečné a nebezpečné");
        code.Should().NotContain("\"default-src 'self' 'unsafe-eval",
            "CSP default-src nesmí obsahovat 'unsafe-eval'");
    }

    [Fact]
    public void Program_Should_Preserve_XTraceId_Header()
    {
        // Regression test: původní X-Trace-Id middleware nesmí být odstraněný
        // při přidání security headers — používá se pro korelaci s logy.
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Program.cs"));

        code.Should().Contain("\"X-Trace-Id\"",
            "X-Trace-Id musí zůstat pro diagnostiku");
        code.Should().Contain("context.TraceIdentifier",
            "X-Trace-Id se musí nastavit z HttpContext.TraceIdentifier");
    }
}
