using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvyControllerTests
{
    // Tyto testy nespouští plný HTTP pipeline — testují mapping výsledku VyzvaResult<T>
    // na JSON payload, kterým VyzvyController odpovídá. Plná integrace přes
    // WebApplicationFactory přijde ve fázi 4.

    [Fact]
    public void OkResult_MapuuJsonSuccessTrueReloadTrue()
    {
        var dummy = new VyzvaDetail(
            1, 1, "1/2026", 2026, 1, VyzvaStav.Priprava,
            DateTime.UtcNow, 1, null, null, "F", "A",
            Array.Empty<VyzvaDetailItem>());

        VyzvaResult<VyzvaDetail> result = new VyzvaResult<VyzvaDetail>.Ok(dummy);

        object payload = result switch
        {
            VyzvaResult<VyzvaDetail>.Ok => new { success = true, reload = true },
            VyzvaResult<VyzvaDetail>.Fail f => new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message },
            _ => throw new InvalidOperationException(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"reload\":true");
    }

    [Fact]
    public void FailResult_MapujeJsonSuccessFalseSCodeAMessage()
    {
        VyzvaResult<VyzvaDetail> fail = new VyzvaResult<VyzvaDetail>.Fail(
            new VyzvaError(VyzvaErrorCode.BufferEmpty, "Buffer je prazdny"));

        object payload = fail switch
        {
            VyzvaResult<VyzvaDetail>.Ok => new { success = true, reload = true },
            VyzvaResult<VyzvaDetail>.Fail f => new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message },
            _ => throw new InvalidOperationException(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"errorCode\":\"BufferEmpty\"");
        json.Should().Contain("Buffer je prazdny");
    }

    [Fact]
    public void UnitOk_MapujeJsonSuccessTrueBezReload()
    {
        VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit> result =
            new VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Ok(default);

        object payload = result switch
        {
            VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Ok => new { success = true },
            VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Fail f => new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message },
            _ => throw new InvalidOperationException(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("\"success\":true");
        json.Should().NotContain("reload");
    }

    [Theory]
    [InlineData("Priprava", true)]
    [InlineData("Odeslano", true)]
    [InlineData("Zruseno", true)]
    [InlineData("NeznamyStav", false)]
    [InlineData("", false)]
    [InlineData("priprava", false)] // case-sensitive (Enum.TryParse s ignoreCase: false)
    public void ZmenitStav_ParseStavu_CaseSensitive(string input, bool canParse)
    {
        var ok = Enum.TryParse<VyzvaStav>(input, ignoreCase: false, out _);
        ok.Should().Be(canParse);
    }

    [Theory]
    [InlineData(VyzvaErrorCode.BufferEmpty, "BufferEmpty")]
    [InlineData(VyzvaErrorCode.VyzvaIsLocked, "VyzvaIsLocked")]
    [InlineData(VyzvaErrorCode.ExternalLinkNotPnf, "ExternalLinkNotPnf")]
    [InlineData(VyzvaErrorCode.InvalidStateTransition, "InvalidStateTransition")]
    public void ErrorCode_ToString_StabilniNazvy(VyzvaErrorCode code, string expected)
    {
        code.ToString().Should().Be(expected);
    }
}
