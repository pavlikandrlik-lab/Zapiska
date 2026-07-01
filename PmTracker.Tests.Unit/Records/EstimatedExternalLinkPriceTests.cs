using FluentAssertions;
using PmTracker.Web.Services;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Fokusovaný test pravidla „předpokládaná cena externí vazby jen pro PMP/PNF"
/// (RecordService.NormalizeEstimatedExternalLinkPrice / SupportsEstimatedExternalLinkPrice).
///
/// Nahrazuje původní end-to-end Save test (RecordEditorControllerTests), který nešlo spustit
/// v Api fixture: vytvoření nové SD vazby přes Save vyžaduje Ticketing.Enabled=true + existující
/// ticket v ServiceDesku, ale testovací prostředí má SD vypnuté. Pravidlo ceny je čistá funkce
/// nezávislá na SD bráně, proto se testuje přímo (2026-06-16).
/// </summary>
public sealed class EstimatedExternalLinkPriceTests
{
    [Theory]
    [InlineData("PMP", true)]
    [InlineData("PNF", true)]
    [InlineData("pmp", true)]   // case-insensitive
    [InlineData("pnf", true)]
    [InlineData("NES", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SupportsEstimatedExternalLinkPrice_OnlyForPmpAndPnf(string? typeCode, bool expected)
    {
        RecordService.SupportsEstimatedExternalLinkPrice(typeCode).Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldKeepPrice_ForPmp()
    {
        RecordService.NormalizeEstimatedExternalLinkPrice("PMP", "125000.50").Should().Be(125000.50m);
    }

    [Fact]
    public void Normalize_ShouldKeepPrice_ForPnf()
    {
        RecordService.NormalizeEstimatedExternalLinkPrice("PNF", "999.99").Should().Be(999.99m);
    }

    [Fact]
    public void Normalize_ShouldDropPrice_ForUnsupportedType_EvenWhenValueProvided()
    {
        // NES (a jakýkoli ne-PMP/PNF) cenu nedrží → null, i když uživatel hodnotu zadal.
        RecordService.NormalizeEstimatedExternalLinkPrice("NES", "999.99").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ShouldReturnNull_ForEmptyPrice_EvenForSupportedType(string? price)
    {
        RecordService.NormalizeEstimatedExternalLinkPrice("PMP", price).Should().BeNull();
    }

    [Fact]
    public void Normalize_ShouldRoundToTwoDecimals_AwayFromZero()
    {
        RecordService.NormalizeEstimatedExternalLinkPrice("PMP", "10.125").Should().Be(10.13m);
    }

    [Fact]
    public void Normalize_ShouldThrow_ForInvalidNumber_OnSupportedType()
    {
        var act = () => RecordService.NormalizeEstimatedExternalLinkPrice("PMP", "abc");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*není validní číslo*");
    }
}
