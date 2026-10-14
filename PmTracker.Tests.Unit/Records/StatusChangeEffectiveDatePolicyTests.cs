using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Nabídka „Změna stavu platí k“ — spec 2026-10-13 §4.2–§4.4, hlášky §7.</summary>
public sealed class StatusChangeEffectiveDatePolicyTests
{
    private static readonly DateTime Today = new(2026, 10, 13);
    private static readonly DateTime FoundedLongAgo = new(2026, 1, 5);

    private static readonly StatusChangeMeetingCandidate M889Draft = new(889, 889, new DateTime(2026, 10, 22), "DRAFT", false);
    private static readonly StatusChangeMeetingCandidate M888Open = new(888, 888, new DateTime(2026, 10, 8), "OPEN", false);
    private static readonly StatusChangeMeetingCandidate M887Open = new(887, 887, new DateTime(2026, 10, 1), "OPEN", false);
    private static readonly StatusChangeMeetingCandidate M886Closed = new(886, 886, new DateTime(2026, 9, 24), "CLOSED", true);
    private static readonly StatusChangeMeetingCandidate M885Closed = new(885, 885, new DateTime(2026, 9, 17), "CLOSED", true);
    private static readonly StatusChangeMeetingCandidate M884Open = new(884, 884, new DateTime(2026, 9, 10), "OPEN", false);

    /// <summary>Příklad ze zadání (spec §4.2) — vstup záměrně neseřazený.</summary>
    private static readonly IReadOnlyList<StatusChangeMeetingCandidate> SpecExample =
        [M884Open, M887Open, M889Draft, M885Closed, M888Open, M886Closed];

    [Fact]
    public void Build_SpecExample_OffersTodayAnd889To887_StopsAtFirstClosed_DefaultsTo888()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        choice.Options.Select(x => x.Label).Should().Equal(
            "Dnes (13.10.2026)",
            "Jednání č. 889 – 22.10.2026 (příprava)",
            "Jednání č. 888 – 08.10.2026",
            "Jednání č. 887 – 01.10.2026");
        choice.Options.Select(x => x.Value).Should().Equal("", "889", "888", "887");
        choice.Options.Select(x => x.JednaniId).Should().Equal(null, 889, 888, 887);
        choice.DefaultValue.Should().Be("888", "výchozí je nejnovější jednání Otevřeno pro zápis (spec §4.3)");
    }

    [Fact]
    public void Build_LockedOpenMeeting_StopsTheOfferLikeClosedOne()
    {
        var locked888 = M888Open with { IsClosed = true };

        var choice = StatusChangeEffectiveDatePolicy.Build([M889Draft, locked888, M887Open], Today, null, FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("", "889");
    }

    [Fact]
    public void Build_UnknownMeetingState_IsSkipped_ButDoesNotStopTheOffer()
    {
        var unknown888 = M888Open with { StavKod = "JINY" };

        var choice = StatusChangeEffectiveDatePolicy.Build([M889Draft, unknown888, M887Open], Today, null, FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("", "889", "887");
    }

    [Fact]
    public void Build_LowerBoundFromLastChange_RemovesEarlierOptions_KeepsSameDate()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 8), FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("", "889", "888");
        choice.LowerBound.Should().Be(new DateTime(2026, 10, 8));
    }

    [Fact]
    public void Build_FoundingDate_IsALowerBoundToo()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, new DateTime(2026, 10, 5));

        choice.Options.Select(x => x.Value).Should().Equal("", "889", "888");
    }

    [Fact]
    public void Build_DefaultFallsBackToToday_WhenNoOpenMeetingIsOffered()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build([M889Draft, M886Closed], Today, null, FoundedLongAgo);

        choice.DefaultValue.Should().Be(StatusChangeEffectiveDatePolicy.TodayValue);
    }

    [Fact]
    public void Build_DefaultFallsBackToFirstOption_WhenTodayIsBelowLowerBound()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("889");
        choice.DefaultValue.Should().Be("889");
    }

    [Fact]
    public void Build_NoOptions_WhenEverythingIsBelowLowerBound()
    {
        var closed889 = M889Draft with { StavKod = "CLOSED", IsClosed = true };

        var choice = StatusChangeEffectiveDatePolicy.Build([closed889, M888Open], Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        choice.Options.Should().BeEmpty();
        choice.DefaultValue.Should().Be(StatusChangeEffectiveDatePolicy.TodayValue);
    }

    [Fact]
    public void Resolve_ReturnsChosenMeeting()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        var resolution = StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, "887");

        resolution.Error.Should().BeNull();
        resolution.Option!.JednaniId.Should().Be(887);
        resolution.Option.Datum.Should().Be(new DateTime(2026, 10, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_MissingValue_MeansToday(string? requested)
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        var resolution = StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, requested);

        resolution.Option!.JednaniId.Should().BeNull();
        resolution.Option.Datum.Should().Be(Today);
    }

    [Theory]
    [InlineData("886")]
    [InlineData("884")]
    public void Resolve_ClosedMeetingOrMeetingBehindClosedOne_IsRejected(string requested)
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        var resolution = StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, requested);

        resolution.Option.Should().BeNull();
        resolution.Error.Should().Be(
            $"Jednání č. {requested} teď nejde zvolit — je uzavřené nebo leží za uzavřeným jednáním. Vyberte znovu, k čemu změna stavu platí.");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    public void Resolve_UnknownMeeting_IsRejected(string requested)
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, requested).Error.Should().Be(
            "Vybrané jednání neexistuje nebo bylo smazáno. Vyberte znovu, k čemu změna stavu platí.");
    }

    [Fact]
    public void Resolve_OptionBelowLowerBound_IsRejected()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 8), FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, "887").Error.Should().Be(
            "Změna stavu nemůže platit dříve než 08.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    [Fact]
    public void Resolve_TodayBelowLowerBound_IsRejected()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, "").Error.Should().Be(
            "Změna stavu nemůže platit dříve než 22.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    [Fact]
    public void Resolve_NoOptions_ExplainsWhyStatusCannotChange()
    {
        var closed889 = M889Draft with { StavKod = "CLOSED", IsClosed = true };
        var meetings = new[] { closed889, M888Open };
        var choice = StatusChangeEffectiveDatePolicy.Build(meetings, Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, meetings, "").Error.Should().Be(
            "Stav teď nejde změnit: změna nemůže platit dříve než 22.10.2026 (předchozí změna stavu nebo založení záznamu) a žádné nabízené datum není pozdější.");
    }
}
