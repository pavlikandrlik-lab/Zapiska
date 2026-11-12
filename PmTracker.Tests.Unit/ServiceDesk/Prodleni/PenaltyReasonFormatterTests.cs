using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk.Prodleni;

public sealed class PenaltyReasonFormatterTests
{
    private static readonly DateTime Od = new(2026, 3, 2, 10, 0, 0);
    private static readonly DateTime Do = new(2026, 3, 9, 14, 0, 0);
    private static readonly DateTime Termin = new(2026, 3, 5, 23, 59, 59);

    public static TheoryData<PenaltyReasonKod> VsechnyKody()
    {
        var data = new TheoryData<PenaltyReasonKod>();
        foreach (var k in Enum.GetValues<PenaltyReasonKod>()) data.Add(k);
        return data;
    }

    [Theory]
    [MemberData(nameof(VsechnyKody))]
    public void Kazdy_kod_ma_vetu_bez_zastupnych_znaku(PenaltyReasonKod kod)
    {
        var text = PenaltyReasonFormatter.Text(new PenaltyReason(kod, Od, Do, Termin, Od, Termin, 4, 16, 4.5));

        text.Should().NotBeNullOrWhiteSpace();
        text.Should().NotContain("{").And.NotContain("T0").And.NotContain("T1");
        text.Should().EndWith(".");
    }

    [Fact]
    public void T01_ukazuje_hodiny_na_dve_desetinna_mista()
    {
        var text = PenaltyReasonFormatter.Text(new PenaltyReason(PenaltyReasonKod.T01, Od, Do, ZbyvaHodin: 16, SpotrebovanoHodin: 4.5));

        text.Should().Contain("16,00 h").And.Contain("4,50 h");
        text.Should().Contain("02.03.2026 10:00").And.Contain("09.03.2026 14:00");
    }

    [Fact]
    public void T05_obsahuje_obratku_termin_a_pocet_dni()
    {
        var text = PenaltyReasonFormatter.Text(new PenaltyReason(PenaltyReasonKod.T05, Od, Do, Deadline: Termin, PocetDni: 4));

        text.Should().Contain("02.03.2026 10:00").And.Contain("09.03.2026 14:00").And.Contain("05.03.2026 23:59").And.Contain("4 dny");
    }

    [Theory]
    [InlineData(0, "0 dní")]
    [InlineData(1, "1 den")]
    [InlineData(2, "2 dny")]
    [InlineData(4, "4 dny")]
    [InlineData(5, "5 dní")]
    [InlineData(21, "21 dní")]
    public void Pocet_dni_ma_spravny_tvar(int dni, string ocekavano)
        => PenaltyReasonFormatter.Text(new PenaltyReason(PenaltyReasonKod.T04, Od, Do, PocetDni: dni))
            .Should().Contain(ocekavano);

    [Fact]
    public void Otevrena_obratka_bez_konce_pise_ted()
        => PenaltyReasonFormatter.Text(new PenaltyReason(PenaltyReasonKod.T01, Od, null, ZbyvaHodin: 16, SpotrebovanoHodin: 2))
            .Should().Contain("teď");

    [Fact]
    public void T14_bez_data_neshodi_text()
        => PenaltyReasonFormatter.Text(new PenaltyReason(PenaltyReasonKod.T14))
            .Should().NotBeNullOrWhiteSpace();
}
