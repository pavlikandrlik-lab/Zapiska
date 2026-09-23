using FluentAssertions;
using PmTracker.Web.Services.Diagnostics;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// Formát diagnostického výpisu je jeden pro AJAX i pro chybovou stránku. Kdyby se
/// rozešly, uživatel by posílal dvě různé podoby téhož a hledalo by se v tom hůř.
/// </summary>
public sealed class DiagnosticLogBuilderTests
{
    [Fact]
    public void Build_ObsahujeKontextIVyjimku()
    {
        var text = DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
            TimestampUtc: new DateTime(2026, 9, 8, 10, 30, 0, DateTimeKind.Utc),
            ErrorCode: "UNHANDLED_EXCEPTION",
            TraceId: "trace-abc",
            RequestLine: "GET /Export/Vyzva/5/Tisk?projektId=2",
            Message: "Tisk selhal.",
            Exception: new InvalidOperationException("boom")));

        text.Should().Contain("2026-09-08");
        text.Should().Contain("UNHANDLED_EXCEPTION");
        text.Should().Contain("trace-abc");
        text.Should().Contain("GET /Export/Vyzva/5/Tisk?projektId=2",
            "bez cesty požadavku není poznat, co spadlo");
        text.Should().Contain("Tisk selhal.");
        text.Should().Contain("InvalidOperationException").And.Contain("boom");
    }

    /// <summary>
    /// QW-7 (2026-04-22) odstranil z výpisu hodnoty formuláře kvůli úniku osobních údajů.
    /// Přesun do sdílené komponenty to nesmí vrátit zpátky.
    /// </summary>
    [Fact]
    public void Build_NeobsahujeSekciSHodnotamiFormulare()
    {
        var text = DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
            TimestampUtc: DateTime.UtcNow,
            ErrorCode: "OPERATION_FAILED",
            TraceId: "t",
            RequestLine: "POST /Projekty/SaveProject",
            Message: "chyba"));

        text.Should().NotContain("FormValues", "hodnoty formuláře nesou osobní údaje");
    }
}
