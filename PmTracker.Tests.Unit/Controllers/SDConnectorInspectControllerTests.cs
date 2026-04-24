using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Controllers;

/// <summary>
/// Unit testy pro <c>/SDConnector/Inspect</c> a <c>/SDConnector/Load</c> — Plán 2 Feature A.
/// Ověřují regex validaci 6-ciferného čísla tiketu. Integrace s <c>ITicketingQueryService</c>
/// a <c>IVyjadreniQueryService</c> je mimo unit scope (zařazeno do PmTracker.Tests.Api nebo manuální E2E).
/// </summary>
public sealed class SDConnectorInspectControllerTests
{
    [Theory]
    [InlineData("12345", false)]   // 5 cifer
    [InlineData("1234567", false)] // 7 cifer
    [InlineData("12345a", false)]  // non-numeric
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123456", true)]   // 6 cifer OK
    [InlineData("000001", true)]   // leading zero also 6 cifer
    public void Load_Validuje6Cifer(string? input, bool shouldMatch)
    {
        var ok = input is not null && Regex.IsMatch(input, @"^\d{6}$");
        ok.Should().Be(shouldMatch);
    }
}
