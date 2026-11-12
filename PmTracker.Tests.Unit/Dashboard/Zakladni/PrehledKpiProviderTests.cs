using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using static PmTracker.Tests.Unit.Dashboard.Zakladni.ZakladniTestData;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>KPI řada „Přehled" (spec 2026-10-15 §11.2, vizuály 1–6).</summary>
public sealed class PrehledKpiProviderTests
{
    private static StatCard Karta(ChartData d, string label) => d.Stats.Single(s => s.Label == label);

    [Fact]
    public void PrehledKpi_otevrene_a_po_terminu()
    {
        var ds = Dataset(
        [
            Otevreny(new DateTime(2026, 3, 1)),
            Otevreny(new DateTime(2026, 3, 30)),
            Otevreny(new DateTime(2026, 4, 10)),
            Otevreny(new DateTime(2026, 5, 10)),
            Otevreny(new DateTime(2026, 3, 31), stav: Novy),
            Otevreny(new DateTime(2026, 1, 1), kategorie: Informace),
        ]);

        var data = new PrehledKpiProvider().Build(ds);

        data.Kind.Should().Be(ChartKind.Kpi);
        data.Key.Should().Be("prehled-kpi");
        var karta = Karta(data, "Otevřené úkoly");
        karta.Value.Should().Be("5");
        karta.Note.Should().Be("z toho 2 po termínu ukončení", "dlaždice souhrnu „Úkoly po termínu“ znamená zpožděný krok harmonogramu");
        karta.Status.Should().Be(CardStatus.Warning);
    }

    [Fact]
    public void PrehledKpi_dokonceno_trend()
    {
        var ds = Dataset(
        [
            .. Enumerable.Range(1, 6).Select(i => Hotovy_(new DateTime(2026, 2, i))),
            .. Enumerable.Range(1, 4).Select(i => Hotovy_(new DateTime(2025, 11, i))),
            Hotovy_(new DateTime(2026, 2, 20), stav: Zruseny),
        ]);

        var karta = Karta(new PrehledKpiProvider().Build(ds), "Dokončeno v období");

        karta.Value.Should().Be("6");
        karta.Trend.Should().NotBeNull();
        karta.Trend!.Delta.Should().Be(2);
        karta.Trend.GoodWhen.Should().Be(TrendPolarity.HigherIsBetter);
        karta.Trend.JeLepsi.Should().BeTrue();
        karta.Trend.Text.Should().Contain("Q4 2025");
    }

    [Fact]
    public void PrehledKpi_vcasnost_trend_dolu()
    {
        var ds = Dataset(
        [
            Hotovy_(new DateTime(2026, 2, 1), termin: new DateTime(2026, 2, 5)),
            Hotovy_(new DateTime(2026, 2, 2), termin: new DateTime(2026, 2, 5)),
            Hotovy_(new DateTime(2026, 2, 3), termin: new DateTime(2026, 2, 5)),
            Hotovy_(new DateTime(2026, 2, 9), termin: new DateTime(2026, 2, 5)),
            .. Enumerable.Range(1, 4).Select(i => Hotovy_(new DateTime(2025, 11, i), termin: new DateTime(2025, 12, 1))),
        ]);

        var karta = Karta(new PrehledKpiProvider().Build(ds), "Včasnost");

        karta.Value.Should().Be("75,0 %");
        karta.Trend!.Delta.Should().Be(-25);
        karta.Trend.JeLepsi.Should().BeFalse();
        karta.Trend.JeHorsi.Should().BeTrue();
    }

    [Fact]
    public void PrehledKpi_posun_terminu_roste_je_hur()
    {
        var ds = Dataset(
        [
            Hotovy_(new DateTime(2026, 2, 10), termin: new DateTime(2026, 2, 20)),
            Hotovy_(new DateTime(2025, 11, 10), termin: new DateTime(2025, 11, 14)),
        ]) with
        {
            TerminChanges = [],
        };
        var ids = ds.Records.Select(r => r.Id).ToList();
        ds = ds with
        {
            TerminChanges =
            [
                new DatasetTerminChange(ids[0], new DateTime(2026, 1, 20), new DateTime(2026, 2, 10), new DateTime(2026, 2, 20)),
                new DatasetTerminChange(ids[1], new DateTime(2025, 10, 20), new DateTime(2025, 11, 10), new DateTime(2025, 11, 14)),
            ],
        };

        var karta = Karta(new PrehledKpiProvider().Build(ds), "Ø posun termínu");

        karta.Value.Should().Be("10 dní");
        karta.Trend!.Delta.Should().Be(6);
        karta.Trend.GoodWhen.Should().Be(TrendPolarity.LowerIsBetter);
        karta.Trend.JeHorsi.Should().BeTrue();
    }

    [Fact]
    public void PrehledKpi_bez_dokoncenych_nema_vcasnost_ani_trend()
    {
        var karta = Karta(new PrehledKpiProvider().Build(Dataset()), "Včasnost");

        karta.Value.Should().Be("—");
        karta.Trend.Should().BeNull();
    }

    [Fact]
    public void PrehledKpi_bez_sd_jen_ctyri_karty()
    {
        var data = new PrehledKpiProvider().Build(Dataset());

        data.Stats.Should().HaveCount(4);
        data.Stats.Select(s => s.Label).Should().Equal("Otevřené úkoly", "Dokončeno v období", "Včasnost", "Ø posun termínu");
    }

    [Fact]
    public void PrehledKpi_rozpocet_progress()
    {
        var ds = Dataset() with
        {
            ServiceDesk = new ServiceDeskSlice(
                ProdleniPrehled.Prazdny(ProdleniStavDat.Ok, 1, "FIS"),
                new IsRozpocetDto(1, "FIS", 1_000_000m, 640_000m), []),
        };

        var data = new PrehledKpiProvider().Build(ds);

        data.Stats.Should().HaveCount(6);
        var karta = Karta(data, "Rozpočet IS");
        karta.Value.Should().Be("64 %");
        karta.Progress.Should().BeApproximately(0.64, 0.0001);
        // cs-CZ odděluje tisíce nezlomitelnou mezerou (podle ICU U+00A0 nebo U+202F) — porovnává se po normalizaci.
        var poznamka = new string(karta.Note!.Select(c => char.IsWhiteSpace(c) || c == '\u202F' ? ' ' : c).ToArray());
        poznamka.Should().Contain("640 000 Kč").And.Contain("1 000 000 Kč");
        Karta(data, "Tickety v prodlení").Value.Should().Be("0");
    }

    [Fact]
    public void PrehledKpi_sd_nedostupny_ukaze_pomlcku()
    {
        var ds = Dataset() with
        {
            ServiceDesk = new ServiceDeskSlice(ProdleniPrehled.Prazdny(ProdleniStavDat.Nedostupne, 1, "FIS"), null, []),
        };

        var karta = Karta(new PrehledKpiProvider().Build(ds), "Tickety v prodlení");

        karta.Value.Should().Be("—");
        karta.Note.Should().Contain("neodpovídá");
    }
}
