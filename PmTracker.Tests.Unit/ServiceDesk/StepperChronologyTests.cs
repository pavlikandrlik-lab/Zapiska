using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Port logiky z PmTracker.Web/wwwroot/js/components/pm-chat-stepper/chronology.js —
/// single source of truth jsou JS funkce, tato C# verze je pro automatizovaný regresní test.
/// JS test infra (Jest/Karma/Jasmine) v repu není, takže chronologie validace se testuje
/// přes paralelní C# implementaci. Pokud se logika v JS změní, update i tuto třídu.
/// </summary>
public sealed class StepperChronologyTests
{
    private static readonly DateTime D1 = new DateTime(2026, 3, 1);
    private static readonly DateTime D2 = new DateTime(2026, 3, 10);
    private static readonly DateTime D3 = new DateTime(2026, 3, 20);

    private sealed record KrokState(int Poradi, DateTime? BindingDatum);
    private sealed record DropResult(bool Ok, string? Reason, int[]? Cascade);

    private static DropResult ValidateDrop(DateTime bubble, KrokState target, KrokState[] allKroky)
    {
        foreach (var k in allKroky)
        {
            if (k.Poradi >= target.Poradi) continue;
            if (k.BindingDatum is DateTime bd && bd > bubble)
                return new(false, $"Krok #{k.Poradi} má binding z {bd:yyyy-MM-dd}", null);
        }
        var cascade = new List<int>();
        foreach (var k in allKroky)
        {
            if (k.Poradi <= target.Poradi) continue;
            if (k.BindingDatum is DateTime bd && bd < bubble)
                cascade.Add(k.Poradi);
        }
        return new(true, null, cascade.Count > 0 ? cascade.ToArray() : null);
    }

    [Fact]
    public void Drop_Target_IsFirstEmpty_NoConflict_Ok()
    {
        var kroky = new[] { new KrokState(1, null), new KrokState(2, null), new KrokState(3, null) };
        var r = ValidateDrop(D2, kroky[0], kroky);
        r.Ok.Should().BeTrue();
        r.Cascade.Should().BeNull();
    }

    [Fact]
    public void Drop_Target_HasEarlierKrok_WithLaterDatum_Blocks()
    {
        var kroky = new[] { new KrokState(1, D3), new KrokState(2, null), new KrokState(3, null) };
        var r = ValidateDrop(D2, kroky[1], kroky);
        r.Ok.Should().BeFalse();
        r.Reason.Should().Contain("Krok #1");
    }

    [Fact]
    public void Drop_Target_HasLaterKrok_WithEarlierDatum_CascadesDown()
    {
        var kroky = new[] { new KrokState(1, null), new KrokState(2, null), new KrokState(3, D1) };
        var r = ValidateDrop(D2, kroky[1], kroky);
        r.Ok.Should().BeTrue();
        r.Cascade.Should().Equal(3);
    }

    [Fact]
    public void Drop_Bubble_Equals_ExistingBinding_Ok()
    {
        var kroky = new[] { new KrokState(1, D1), new KrokState(2, null) };
        var r = ValidateDrop(D1, kroky[1], kroky);
        r.Ok.Should().BeTrue();
    }
}
