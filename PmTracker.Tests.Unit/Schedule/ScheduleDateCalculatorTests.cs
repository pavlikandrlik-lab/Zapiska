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

        var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026, 2, 1));

        res[0].PlanStart.Should().Be(Start);
        res[0].PlanEnd.Should().Be(new DateTime(2026, 1, 10));
        res[1].PlanStart.Should().Be(new DateTime(2026, 1, 10));
        res[1].PlanEnd.Should().Be(new DateTime(2026, 1, 25));
    }

    // --- SKUTEČNOST: jen vyplněné; segment k = [skut(předchozí vyplněný), skut(k)] ---

    [Fact]
    public void Compute_Skutecnost_jen_vyplnene_prostredni_se_preskoci()
    {
        // vyplněno 1 a 3; krok 2 nevyplněn (mezera PŘED posledním vyplněným) → segment 3 = [skut(1), skut(3)]
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12)),
            Krok(2, new DateTime(2026, 1, 20), null),
            Krok(3, new DateTime(2026, 1, 30), new DateTime(2026, 2, 5)),
        };

        var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026, 3, 1));

        res[0].MaSkutecnost.Should().BeTrue();
        res[0].SkutecnostStart.Should().Be(Start);
        res[0].SkutecnostEnd.Should().Be(new DateTime(2026, 1, 12));

        res[1].MaSkutecnost.Should().BeFalse();   // krok 2 je mezera před posledním vyplněným → nekreslí se

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

        var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026, 2, 1));

        res[1].SkutecnostEnd.Should().BeOnOrAfter(res[1].SkutecnostStart);
    }

    // --- STAV + AKTUÁLNÍ KROK ---

    [Fact]
    public void Compute_stav_ceka_vprodleni_splneno()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 11)), // Splneno
            Krok(2, new DateTime(2026, 1, 20), null),                      // nevyplněn, plán < dnes → VProdleni (aktuální)
            Krok(3, new DateTime(2026, 3, 20), null),                      // nevyplněn, plán > dnes → Ceka
        };

        var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026, 2, 1));

        res[0].Stav.Should().Be(HarmonogramKrokStav.Splneno);
        res[1].Stav.Should().Be(HarmonogramKrokStav.VProdleni);
        res[2].Stav.Should().Be(HarmonogramKrokStav.Ceka);
    }

    [Fact]
    public void Compute_aktualni_krok_ma_actual_segment_do_dneska()
    {
        // vyplněno krok 1 do 2026-01-11; aktuální krok 2, dnes 2026-02-01
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 11)),
            Krok(2, new DateTime(2026, 1, 20), null),
        };

        var res = ScheduleDateCalculator.Compute(Start, steps, today: new DateTime(2026, 2, 1));

        res[1].MaSkutecnost.Should().BeTrue("aktuální krok se kreslí jako rozpracovaný");
        res[1].JeAktualniKrok.Should().BeTrue();
        res[1].SkutecnostStart.Should().Be(new DateTime(2026, 1, 11)); // konec posledního vyplněného
        res[1].SkutecnostEnd.Should().Be(new DateTime(2026, 2, 1));     // dnešek
    }

    // --- SOUHRN: aktuální krok + znaménkové překročení ---

    [Fact]
    public void Summarize_aktualni_krok_je_prvni_nevyplneny_po_poslednim_vyplnenem()
    {
        // vyplněno 1..4, 5..10 prázdné → aktuální = 5
        var steps = Enumerable.Range(1, 10).Select(i => Krok(
            i, new DateTime(2026, 1, 1).AddDays(i * 10),
            i <= 4 ? new DateTime(2026, 1, 1).AddDays(i * 10) : (DateTime?)null)).ToList();

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 3, 1));

        s.AktualniKrokPoradi.Should().Be(5);
        s.Dokonceno.Should().BeFalse();
    }

    [Fact]
    public void Summarize_aktualni_krok_preskoci_mezery_vyplneno_4_6_9_da_10()
    {
        DateTime? F(int i) => (i is 4 or 6 or 9) ? new DateTime(2026, 1, 1).AddDays(i) : (DateTime?)null;
        var steps = Enumerable.Range(1, 10).Select(i => Krok(i, new DateTime(2026, 1, 1).AddDays(i), F(i))).ToList();

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 3, 1));

        s.AktualniKrokPoradi.Should().Be(10);
    }

    [Fact]
    public void Summarize_prekroceni_je_dnes_minus_plan_aktualniho_kroku_znamenkove()
    {
        // aktuální krok 5; plán(5) = 2026-02-06, dnes = +5
        var steps = Enumerable.Range(1, 10).Select(i => Krok(
            i, new DateTime(2026, 2, 1).AddDays(i),
            i <= 4 ? new DateTime(2026, 2, 1).AddDays(i) : (DateTime?)null)).ToList();
        var planP5 = new DateTime(2026, 2, 1).AddDays(5);

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 12, 1), today: planP5.AddDays(5));

        s.PrekroceniDni.Should().Be(5);
    }

    [Fact]
    public void Summarize_predstih_je_zaporne_prekroceni()
    {
        var steps = Enumerable.Range(1, 10).Select(i => Krok(
            i, new DateTime(2026, 2, 1).AddDays(i),
            i <= 4 ? new DateTime(2026, 2, 1).AddDays(i) : (DateTime?)null)).ToList();
        var planP5 = new DateTime(2026, 2, 1).AddDays(5);

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 12, 1), today: planP5.AddDays(-3));

        s.PrekroceniDni.Should().Be(-3);
    }

    [Fact]
    public void Summarize_vsechny_vyplnene_je_dokonceno_a_prekroceni_nula()
    {
        var steps = Enumerable.Range(1, 10).Select(i => Krok(
            i, new DateTime(2026, 1, 1).AddDays(i), new DateTime(2026, 1, 1).AddDays(i))).ToList();

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 3, 1));

        s.Dokonceno.Should().BeTrue();
        s.PrekroceniDni.Should().Be(0);
    }

    [Fact]
    public void Summarize_zadny_vyplneny_aktualni_je_krok_1()
    {
        var steps = Enumerable.Range(1, 10).Select(i => Krok(i, new DateTime(2026, 1, 1).AddDays(i), (DateTime?)null)).ToList();

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 1, 5));

        s.AktualniKrokPoradi.Should().Be(1);
    }

    [Fact]
    public void Summarize_planove_dokonceni_je_plan_posledniho_kroku()
    {
        var steps = new[]
        {
            Krok(1, new DateTime(2026, 1, 10), null),
            Krok(2, new DateTime(2026, 1, 25), null),
        };

        var s = ScheduleDateCalculator.Summarize(Start, steps, termin: new DateTime(2026, 1, 25), today: new DateTime(2026, 1, 15));

        s.PlanoveDokonceni.Should().Be(new DateTime(2026, 1, 25));
        s.Termin.Should().Be(new DateTime(2026, 1, 25));
    }
}
