using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedules;

/// <summary>
/// Spec 2026-10-14 §3 — pravidla zápisu skutečnosti harmonogramu. Jedna komponenta pro
/// uložení editoru i schválení návrhu, proto se testuje bez DB nad samotnými řádky kroků.
/// </summary>
public sealed class ScheduleActualApplierTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime A = new(2026, 10, 5);
    private static readonly DateTime B = new(2026, 10, 9);

    private static ZaznamHarmonogramKrokEntity Krok(int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
        => new()
        {
            ZaznamId = 1,
            Poradi = (byte)poradi,
            SkutecnostDatum = datum,
            SkutecnostZdroj = (byte)zdroj,
            SkutecnostRezim = (byte)rezim
        };

    private static (List<ZaznamHarmonogramKrokEntity> Rows, Func<int, ZaznamHarmonogramKrokEntity> GetOrCreate) Radky(
        params ZaznamHarmonogramKrokEntity[] seed)
    {
        var rows = seed.ToList();
        ZaznamHarmonogramKrokEntity GetOrCreate(int poradi)
        {
            var row = rows.FirstOrDefault(x => x.Poradi == poradi);
            if (row is not null) return row;
            row = new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = 1,
                Poradi = (byte)poradi,
                SkutecnostRezim = ScheduleActualApplier.VychoziRezim(poradi),
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo
            };
            rows.Add(row);
            return row;
        }
        return (rows, GetOrCreate);
    }

    private static ScheduleActualApplier.Vysledek Apply(
        (List<ZaznamHarmonogramKrokEntity> Rows, Func<int, ZaznamHarmonogramKrokEntity> GetOrCreate) r,
        string? rezim,
        params ScheduleActualApplier.SkutecnostZmena[] zmeny)
        => ScheduleActualApplier.Apply(r.Rows.ToList(), r.GetOrCreate, zmeny, rezim, Now);

    private static ScheduleActualApplier.SkutecnostZmena Z(int poradi, DateTime? datum) => new(poradi, datum);

    [Fact]
    public void RucniKrok_Zmena_ZapiseDatumRucne()
    {
        var r = Radky();
        Apply(r, "Auto", Z(5, A));
        var k5 = r.Rows.Single(x => x.Poradi == 5);
        k5.SkutecnostDatum.Should().Be(A);
        k5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
        k5.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual, "kroky 2/5/8/9 jsou vždy ruční (R3)");
    }

    [Fact]
    public void RucniKrok_Smazani_VyprazdniKrok()
    {
        var r = Radky(Krok(5, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual));
        Apply(r, "Auto", Z(5, null));
        var k5 = r.Rows.Single(x => x.Poradi == 5);
        k5.SkutecnostDatum.Should().BeNull();
        k5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
    }

    [Fact]
    public void AutoKrok_ZmenaVRucnimRezimu_Zapise()
    {
        var r = Radky();
        Apply(r, "Manual", Z(3, B));
        var k3 = r.Rows.Single(x => x.Poradi == 3);
        k3.SkutecnostDatum.Should().Be(B);
        k3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
    }

    [Fact]
    public void AutoKrok_ZmenaVAutoRezimu_Ignoruje()
    {
        var r = Radky(Krok(3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto));
        Apply(r, "Auto", Z(3, B));
        var k3 = r.Rows.Single(x => x.Poradi == 3);
        k3.SkutecnostDatum.Should().Be(A, "v automatickém režimu krok řídí automat");
        k3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
    }

    /// <summary>D3 — vypnutí automatiky a uložení bez úprav nesmí smazat data automatu.</summary>
    [Fact]
    public void PrepnutiNaRucni_NedotcenyKrok_ZachovaHodnotuIZdroj()
    {
        var r = Radky(
            Krok(1, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto),
            Krok(3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto));
        Apply(r, "Manual");
        foreach (var poradi in new[] { 1, 3 })
        {
            var k = r.Rows.Single(x => x.Poradi == poradi);
            k.SkutecnostDatum.Should().Be(A);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        }
    }

    /// <summary>R2 — přepnutí na automatiku smaže vše, co nedodal automat (ruční i historická data).</summary>
    [Fact]
    public void PrepnutiNaAuto_SmazeRucniIHistorickaData_NechaAutomat()
    {
        var r = Radky(
            Krok(1, A, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Manual),
            Krok(3, B, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual),
            Krok(4, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Manual));
        var vysledek = Apply(r, "Auto");

        vysledek.PrepnutoNaAuto.Should().BeTrue();
        foreach (var poradi in new[] { 1, 3 })
        {
            var k = r.Rows.Single(x => x.Poradi == poradi);
            k.SkutecnostDatum.Should().BeNull("v automatickém režimu nesmí zůstat datum, které nedodal automat");
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
        }
        r.Rows.Single(x => x.Poradi == 4).SkutecnostDatum.Should().Be(A, "data automatu zůstávají");
        r.Rows.Where(x => x.Poradi is 1 or 3 or 4)
            .Should().OnlyContain(x => x.SkutecnostRezim == (byte)SkutecnostRezimEnum.Auto);
    }

    /// <summary>R2 — smíšený stav (část kroků už Auto s ručním datem) se přepnutím vyčistí celý.</summary>
    [Fact]
    public void PrepnutiNaAuto_ZeSmisenehoStavu_VycistiIKrokyUzVAuto()
    {
        var r = Radky(
            Krok(3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual),
            Krok(6, B, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Auto));
        Apply(r, "Auto").PrepnutoNaAuto.Should().BeTrue();
        r.Rows.Single(x => x.Poradi == 6).SkutecnostDatum.Should().BeNull("nic mezi — po zapnutí automatiky jen data automatu");
    }

    /// <summary>Záznam už v automatickém režimu: uložení s Auto není přepnutí, nic se nemaže ani nestahuje.</summary>
    [Fact]
    public void ZaznamUzVAuto_CilAuto_NicNemeni()
    {
        var r = Radky(Krok(6, B, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Auto));
        var vysledek = Apply(r, "Auto");
        vysledek.Should().Be(new ScheduleActualApplier.Vysledek(false, false));
        r.Rows.Single(x => x.Poradi == 6).SkutecnostDatum.Should().Be(B);
    }

    /// <summary>R3 — přepínač nesmí sáhnout na kroky 2/5/8/9.</summary>
    [Theory]
    [InlineData("Auto")]
    [InlineData("Manual")]
    public void PrepnutiRezimu_NemeniKroky2589(string rezim)
    {
        var r = Radky(Krok(5, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual));
        Apply(r, rezim);
        var k5 = r.Rows.Single(x => x.Poradi == 5);
        k5.SkutecnostDatum.Should().Be(A);
        k5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
        k5.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
    }

    /// <summary>Review Focus 5 — úkol bez řádků kroků.</summary>
    [Fact]
    public void PrepnutiNaRucni_BezRadku_ZalozeRadkyAutoKroku()
    {
        var r = Radky();
        Apply(r, "Manual");
        r.Rows.Select(x => (int)x.Poradi).Should().BeEquivalentTo(new[] { 1, 3, 4, 6, 7, 10 });
        r.Rows.Should().OnlyContain(x => x.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual);
    }

    [Fact]
    public void BezCilovehoRezimu_RidiSeStavemZaznamu()
    {
        var rucni = Radky(Krok(1, null, SkutecnostZdrojEnum.Neznamo, SkutecnostRezimEnum.Manual));
        Apply(rucni, null, Z(3, B));
        rucni.Rows.Single(x => x.Poradi == 3).SkutecnostDatum.Should().Be(B, "záznam je v ručním režimu");

        var auto = Radky(Krok(1, null, SkutecnostZdrojEnum.Neznamo, SkutecnostRezimEnum.Auto));
        Apply(auto, null, Z(3, B));
        auto.Rows.Should().NotContain(x => x.Poradi == 3 && x.SkutecnostDatum == B, "záznam je v automatickém režimu");
    }

    [Fact]
    public void JeZmena_PorovnavaPoDnech()
    {
        ScheduleActualApplier.JeZmena(new DateTime(2026, 10, 5, 13, 0, 0), new DateTime(2026, 10, 5)).Should().BeFalse();
        ScheduleActualApplier.JeZmena(null, null).Should().BeFalse();
        ScheduleActualApplier.JeZmena(null, A).Should().BeTrue("smazání je změna");
        ScheduleActualApplier.JeZmena(A, null).Should().BeTrue();
    }
}
