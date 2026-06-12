using FluentAssertions;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleDateCalculatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static ScheduleDateStep Krok(int poradi, DateTime? plan, DateTime? skutecnost)
        => new(poradi, plan, skutecnost);

    // --- PLÁN: segment k = [plan(k-1), plan(k)], plan(0)=start ---

    [Fact]
    public void Compute_Plan_segmenty_navazuji_od_startu()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), null),
            Krok(2, new DateTime(2026, 1, 25), null),
        };

        var res = ScheduleDateCalculator.Compute(Start, steps);

        res[0].PlanStart.Should().Be(Start);
        res[0].PlanEnd.Should().Be(new DateTime(2026, 1, 10));
        res[1].PlanStart.Should().Be(new DateTime(2026, 1, 10));
        res[1].PlanEnd.Should().Be(new DateTime(2026, 1, 25));
    }

    // --- SKUTEČNOST: jen vyplněné; segment k = [skut(předchozí vyplněný), skut(k)] ---

    [Fact]
    public void Compute_Skutecnost_jen_vyplnene_prostredni_se_preskoci()
    {
        // vyplněno 1 a 3; krok 2 nevyplněn → segment 3 = [skut(1), skut(3)]
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12)),
            Krok(2, new DateTime(2026, 1, 20), null),
            Krok(3, new DateTime(2026, 1, 30), new DateTime(2026, 2, 5)),
        };

        var res = ScheduleDateCalculator.Compute(Start, steps);

        res[0].MaSkutecnost.Should().BeTrue();
        res[0].SkutecnostStart.Should().Be(Start);
        res[0].SkutecnostEnd.Should().Be(new DateTime(2026, 1, 12));

        res[1].MaSkutecnost.Should().BeFalse();   // krok 2 se nekreslí

        res[2].MaSkutecnost.Should().BeTrue();
        res[2].SkutecnostStart.Should().Be(new DateTime(2026, 1, 12)); // od konce kroku 1 (předchozí vyplněný)
        res[2].SkutecnostEnd.Should().Be(new DateTime(2026, 2, 5));
    }

    [Fact]
    public void Compute_Skutecnost_mimo_poradi_ma_nezapornou_sirku()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 20)),
            Krok(2, new DateTime(2026, 1, 20), new DateTime(2026, 1, 10)), // dřív než krok 1
        };

        var res = ScheduleDateCalculator.Compute(Start, steps);

        res[1].SkutecnostEnd.Should().BeOnOrAfter(res[1].SkutecnostStart);
    }

    // --- SOUHRN ---

    [Fact]
    public void Summarize_vsechny_vyplnene_skutecne_dokonceni_je_posledni_krok()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12)),
            Krok(2, new DateTime(2026, 1, 25), new DateTime(2026, 1, 28)),
        };
        var termin = new DateTime(2026, 1, 25);

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin, today: new DateTime(2026, 2, 1));

        s.SkutecneDokonceni.Should().Be(new DateTime(2026, 1, 28));
        s.Stihame.Should().BeFalse();
        s.PrekroceniDni.Should().Be(3); // 28 - 25
        s.PlanoveDokonceni.Should().Be(new DateTime(2026, 1, 25));
    }

    [Fact]
    public void Summarize_koncovy_krok_nevyplnen_projektuje_na_dnesek()
    {
        // poslední krok (2) nevyplněn, dnes daleko za skutečností kroku 1 → projekce na dnešek
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12)),
            Krok(2, new DateTime(2026, 1, 25), null),
        };
        var termin = new DateTime(2026, 1, 25);
        var today = new DateTime(2026, 3, 1);

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin, today);

        s.SkutecneDokonceni.Should().Be(today);
        s.Stihame.Should().BeFalse();
        s.PrekroceniDni.Should().Be((today - termin).Days);
    }

    [Fact]
    public void Summarize_v_terminu_stiha()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 9)),
            Krok(2, new DateTime(2026, 1, 25), new DateTime(2026, 1, 24)),
        };
        var termin = new DateTime(2026, 1, 25);

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin, today: new DateTime(2026, 1, 24));

        s.Stihame.Should().BeTrue();
        s.PrekroceniDni.Should().Be(0);
    }
}
