using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk.Prodleni;

/// <summary>Golden testy portu PenaltyCalculation.TicketPenaltyCalculator (Hotline.cs ř. 1327–1967).
/// Hodnoty jsou ověřené ručním průchodem originálem; „teď" je 25.03.2026 12:00, pokud test neříká jinak.</summary>
public sealed class PenaltyCalculatorTests
{
    private static readonly DateTime VychoziTed = new(2026, 3, 25, 12, 0, 0);
    private int _dalsiId = 1;

    private static DateTime D(string s) => DateTime.ParseExact(s,
        ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None);

    private WorkflowUdalost U(int typ, string datum, string? termin = null, string? dodavatel = null, int? id = null)
        => new(id ?? _dalsiId++, D(datum), typ, dodavatel, termin is null ? null : D(termin));

    private static PenaltyInput Pmp(params WorkflowUdalost[] u) => new(1, "PMP", null, null, "A", u);
    private static PenaltyInput Pnf(params WorkflowUdalost[] u) => new(1, "PNF", null, null, "A", u);
    private static PenaltyInput Nes(double? sla, params WorkflowUdalost[] u) => new(1, "NES", "NES II", sla, "A", u);

    private static PenaltyCalculator Calc(DateTime? ted = null)
    {
        var t = new FakeTimeProvider(new DateTimeOffset(ted ?? VychoziTed, TimeSpan.Zero));
        t.SetLocalTimeZone(TimeZoneInfo.Utc);
        return new PenaltyCalculator(new CeskeSvatky(), t);
    }

    private static PenaltyResult Jeden(IReadOnlyList<PenaltyResult> r)
    {
        r.Should().HaveCount(1);
        return r[0];
    }

    private static DateTime[] Dny(params string[] dny) => dny.Select(D).ToArray();
    private static PenaltyReasonKod[] Kody(PenaltyResult r) => r.Duvody.Select(d => d.Kod).ToArray();

    [Fact]
    public void Pmp_pozde_kalendarni_dny_T05()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-09 14:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-06", "2026-03-07", "2026-03-08", "2026-03-09"));
        Kody(r).Should().Equal(PenaltyReasonKod.T05);
        r.PracovniDny.Should().BeFalse();
        r.Dodavatel.Should().Be("A");
        r.MaChybu.Should().BeFalse();
    }

    [Fact]
    public void Pnf_pozde_pracovni_dny_T05()
    {
        var r = Jeden(Calc().Spocitej(Pnf(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-09 14:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-06", "2026-03-09"));
        Kody(r).Should().Equal(PenaltyReasonKod.T05);
        r.PracovniDny.Should().BeTrue();
    }

    [Fact]
    public void Pnf_pres_velikonoce()
    {
        var r = Jeden(Calc(D("2026-04-30 12:00")).Spocitej(Pnf(
            U(TransferTyp.U1, "2026-04-01 09:00", termin: "2026-04-02 23:59:59"),
            U(TransferTyp.U3, "2026-04-08 10:00"))));

        // 03.04. Velký pátek, 04.–05.04. víkend, 06.04. Velikonoční pondělí
        r.PenalizovaneDny.Should().Equal(Dny("2026-04-07", "2026-04-08"));
    }

    [Fact]
    public void Pmp_vcas_T03()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-05 18:00"))));

        r.PenalizovaneDny.Should().BeEmpty();
        Kody(r).Should().Equal(PenaltyReasonKod.T03);
    }

    [Fact]
    public void Pmp_termin_o_pulnoci_00_00()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 00:00"),
            U(TransferTyp.U3, "2026-03-09 10:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-05", "2026-03-06", "2026-03-07", "2026-03-08", "2026-03-09"));
    }

    [Fact]
    public void Nes_v_ramci_sla_T01()
    {
        var r = Jeden(Calc().Spocitej(Nes(16,
            U(TransferTyp.U1, "2026-03-02 08:00"),
            U(TransferTyp.U3, "2026-03-02 12:00"))));

        r.PenalizovaneDny.Should().BeEmpty();
        Kody(r).Should().Equal(PenaltyReasonKod.T01);
        r.Duvody[0].ZbyvaHodin.Should().Be(16);
        r.Duvody[0].SpotrebovanoHodin.Should().Be(4);
        r.PracovniDny.Should().BeTrue();
    }

    [Fact]
    public void Nes_prekroceni_sla_T05()
    {
        var r = Jeden(Calc().Spocitej(Nes(16,
            U(TransferTyp.U1, "2026-03-03 20:00"),
            U(TransferTyp.U3, "2026-03-05 10:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-04", "2026-03-05"));
        Kody(r).Should().Equal(PenaltyReasonKod.T05);
        r.Duvody[0].Deadline.Should().Be(D("2026-03-04 12:00"));
    }

    [Fact]
    public void Nes_po_vycerpani_dalsi_obratka_cela_T04()
    {
        var r = Jeden(Calc().Spocitej(Nes(16,
            U(TransferTyp.U1, "2026-03-03 20:00"),
            U(TransferTyp.U3, "2026-03-05 10:00"),
            U(TransferTyp.U2, "2026-03-09 08:00"),
            U(TransferTyp.U3, "2026-03-11 09:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-04", "2026-03-05", "2026-03-09", "2026-03-10", "2026-03-11"));
        Kody(r).Should().Equal(PenaltyReasonKod.T05, PenaltyReasonKod.T04);
    }

    [Fact]
    public void Nes_sla_zaokrouhleni_convert_toint32()
    {
        // Convert.ToInt32(16.5) == 16 (zaokrouhlení na sudé), stejně jako originál ř. 1428
        var r = Jeden(Calc().Spocitej(Nes(16.5,
            U(TransferTyp.U1, "2026-03-03 20:00"),
            U(TransferTyp.U3, "2026-03-05 10:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-04", "2026-03-05"));
        r.Duvody[0].Deadline.Should().Be(D("2026-03-04 12:00"));
    }

    [Fact]
    public void Nes_dulezitost_bez_sla_vraci_chybu_ne_vyjimku()
    {
        var act = () => Calc().Spocitej(Nes(null,
            U(TransferTyp.U1, "2026-03-03 20:00"),
            U(TransferTyp.U3, "2026-03-05 10:00")));

        var r = Jeden(act.Should().NotThrow().Subject);
        r.Chyba.Should().Be(PenaltyChyba.SlaChybiProDulezitost);
        r.PenalizovaneDny.Should().BeEmpty();
    }

    [Fact]
    public void Nes_u_dodavatele_po_vycerpani_sla_T02_T05()
    {
        // 23.03. 08:00–24:00 = 16 h → deadline 24.03. 00:00; ticket stále u dodavatele, teď 25.03. 12:00
        var r = Jeden(Calc().Spocitej(Nes(16, U(TransferTyp.U1, "2026-03-23 08:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-24", "2026-03-25"));
        Kody(r).Should().Equal(PenaltyReasonKod.T02, PenaltyReasonKod.T05);
        r.Duvody[1].ObratkaDo.Should().Be(VychoziTed);
    }

    [Fact]
    public void Pmp_prodlouzeni_po_zadosti_vcas_T11_T14()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U10, "2026-03-04 10:00"),
            U(TransferTyp.U11, "2026-03-05 10:00", termin: "2026-03-12 23:59:59"),
            U(TransferTyp.U3, "2026-03-10 10:00"))));

        r.PenalizovaneDny.Should().BeEmpty();
        Kody(r).Should().Equal(PenaltyReasonKod.T11, PenaltyReasonKod.T14);
    }

    [Fact]
    public void Pmp_prodlouzeni_po_zadosti_pozde_T12()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U10, "2026-03-04 10:00"),
            U(TransferTyp.U11, "2026-03-05 10:00", termin: "2026-03-08 23:59:59"),
            U(TransferTyp.U3, "2026-03-10 10:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-09", "2026-03-10"));
        Kody(r).Should().Equal(PenaltyReasonKod.T12, PenaltyReasonKod.T14);
    }

    [Fact]
    public void Pmp_otevreny_ticket_prominuti_T14()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U11, "2026-03-06 10:00", termin: "2026-03-20 23:59:59"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-21", "2026-03-22", "2026-03-23", "2026-03-24", "2026-03-25"));
        Kody(r).Should().Equal(PenaltyReasonKod.T02, PenaltyReasonKod.T05, PenaltyReasonKod.T14);
    }

    [Fact]
    public void Pmp_prodlouzeni_bez_zadosti_vcas_T06()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U11, "2026-03-06 10:00", termin: "2026-03-12 23:59:59"),
            U(TransferTyp.U3, "2026-03-10 10:00"))));

        r.PenalizovaneDny.Should().BeEmpty();
        Kody(r).Should().Equal(PenaltyReasonKod.T06, PenaltyReasonKod.T14);
    }

    [Fact]
    public void Pmp_prodlouzeni_bez_zadosti_pozde_T07()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U11, "2026-03-06 10:00", termin: "2026-03-08 23:59:59"),
            U(TransferTyp.U3, "2026-03-10 10:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-09", "2026-03-10"));
        Kody(r).Should().Equal(PenaltyReasonKod.T07, PenaltyReasonKod.T14);
    }

    [Fact]
    public void Nes_druha_obratka_s_prodlouzenim_po_zadosti_T08_T11_prominuti_celku()
    {
        // Druhá obrátka je řízená dny (deadline null) → T08; U11 do 12.03. → T11; T14 smaže i dny první obrátky.
        var r = Jeden(Calc().Spocitej(Nes(16,
            U(TransferTyp.U1, "2026-03-03 20:00"),
            U(TransferTyp.U3, "2026-03-05 10:00"),
            U(TransferTyp.U2, "2026-03-09 08:00"),
            U(TransferTyp.U10, "2026-03-09 10:00"),
            U(TransferTyp.U11, "2026-03-09 11:00", termin: "2026-03-12 23:59:59"),
            U(TransferTyp.U3, "2026-03-11 09:00"))));

        r.PenalizovaneDny.Should().BeEmpty();
        Kody(r).Should().Equal(PenaltyReasonKod.T05, PenaltyReasonKod.T08, PenaltyReasonKod.T11, PenaltyReasonKod.T14);
        r.Duvody[1].Deadline.Should().Be(D("2026-03-09 08:00"));
    }

    [Fact]
    public void Pulnoc_23_59_59_pocita_od_dalsiho_dne()
    {
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-06 00:30"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-06"));
    }

    [Fact]
    public void Den_se_nepenalizuje_dvakrat()
    {
        // Obě obrátky po termínu zasahují 06.03.; den se započítá jen jednou.
        var r = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-06 10:00"),
            U(TransferTyp.U2, "2026-03-06 12:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-07 10:00"))));

        r.PenalizovaneDny.Should().Equal(Dny("2026-03-06", "2026-03-07"));
        r.Duvody.Select(d => d.PocetDni).Should().Equal(1, 1);
    }

    [Fact]
    public void Vice_dodavatelu_T00()
    {
        var r = Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59", dodavatel: "A"),
            U(TransferTyp.U3, "2026-03-04 10:00"),
            U(TransferTyp.U2, "2026-03-05 10:00", termin: "2026-03-06 23:59:59", dodavatel: "B"),
            U(TransferTyp.U3, "2026-03-09 10:00")));

        r.Should().HaveCount(2);
        r[0].Dodavatel.Should().Be("A");
        r[0].PenalizovaneDny.Should().BeEmpty();
        Kody(r[0]).Should().Equal(PenaltyReasonKod.T00, PenaltyReasonKod.T03);
        r[1].Dodavatel.Should().Be("B");
        r[1].PenalizovaneDny.Should().Equal(Dny("2026-03-07", "2026-03-08", "2026-03-09"));
        Kody(r[1]).Should().Equal(PenaltyReasonKod.T00, PenaltyReasonKod.T05);
    }

    [Fact]
    public void Stejny_cas_udalosti_razeni_podle_id()
    {
        var u1 = U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59", id: 10);
        var u3 = U(TransferTyp.U3, "2026-03-02 10:00", id: 11);
        var u2 = U(TransferTyp.U2, "2026-03-03 10:00", termin: "2026-03-04 23:59:59", id: 12);
        var u3b = U(TransferTyp.U3, "2026-03-06 10:00", id: 13);

        var vPoradi = Jeden(Calc().Spocitej(Pmp(u1, u3, u2, u3b)));
        var prehazene = Jeden(Calc().Spocitej(Pmp(u3b, u3, u2, u1)));

        Kody(vPoradi).Should().Equal(PenaltyReasonKod.T03, PenaltyReasonKod.T05);
        vPoradi.PenalizovaneDny.Should().Equal(Dny("2026-03-05", "2026-03-06"));
        Kody(prehazene).Should().Equal(Kody(vPoradi));
        prehazene.PenalizovaneDny.Should().Equal(vPoradi.PenalizovaneDny);
    }

    [Fact]
    public void Neznamy_kod_predavky_se_ignoruje()
    {
        var bez = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59", id: 1),
            U(TransferTyp.U3, "2026-03-09 14:00", id: 3))));
        var s4 = Jeden(Calc().Spocitej(Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59", id: 1),
            U(4, "2026-03-04 10:00", id: 2),
            U(TransferTyp.U3, "2026-03-09 14:00", id: 3))));

        s4.PenalizovaneDny.Should().Equal(bez.PenalizovaneDny);
        Kody(s4).Should().Equal(Kody(bez));
    }

    [Fact]
    public void Zadne_udalosti_chyba()
    {
        var r = Jeden(Calc().Spocitej(Pmp()));
        r.Chyba.Should().Be(PenaltyChyba.ZadneUdalosti);
        r.MaChybu.Should().BeTrue();
    }

    [Fact]
    public void Bez_U1_chyba()
        => Jeden(Calc().Spocitej(Pmp(U(TransferTyp.U3, "2026-03-09 14:00"))))
            .Chyba.Should().Be(PenaltyChyba.ChybiIniciacniUdalost);

    [Fact]
    public void Typ_s_mezerami_char5()
    {
        var input = Pmp(
            U(TransferTyp.U1, "2026-03-02 10:00", termin: "2026-03-05 23:59:59"),
            U(TransferTyp.U3, "2026-03-09 14:00")) with { TypZaznamu = "pmp  " };

        var r = Jeden(Calc().Spocitej(input));
        r.MaChybu.Should().BeFalse();
        r.PenalizovaneDny.Should().HaveCount(4);
    }

    [Fact]
    public void Neznamy_typ_chyba()
    {
        var input = Pmp(U(TransferTyp.U1, "2026-03-02 10:00")) with { TypZaznamu = "RU" };
        Jeden(Calc().Spocitej(input)).Chyba.Should().Be(PenaltyChyba.NeznamyTyp);
    }

    [Fact]
    public void AktualniDeadline_nes_u_dodavatele()
        => Calc().AktualniDeadline(Nes(16, U(TransferTyp.U1, "2026-03-25 08:00")))
            .Should().Be(D("2026-03-26 00:00"));

    [Fact]
    public void AktualniDeadline_nes_pres_vikend()
        => Calc(D("2026-03-28 10:00")).AktualniDeadline(Nes(16, U(TransferTyp.U1, "2026-03-27 20:00")))
            .Should().Be(D("2026-03-30 12:00"));

    [Fact]
    public void AktualniDeadline_nes_odecita_spotrebu_predchozich_obratek()
    {
        // První obrátka spotřebuje 4 h, zbývá 12 h → od 25.03. 08:00 je deadline 25.03. 20:00.
        var d = Calc().AktualniDeadline(Nes(16,
            U(TransferTyp.U1, "2026-03-02 08:00"),
            U(TransferTyp.U3, "2026-03-02 12:00"),
            U(TransferTyp.U2, "2026-03-25 08:00")));
        d.Should().Be(D("2026-03-25 20:00"));
    }

    [Fact]
    public void AktualniDeadline_pmp_bere_termin_nebo_schvalene_prodlouzeni()
    {
        Calc().AktualniDeadline(Pmp(U(TransferTyp.U1, "2026-03-20 10:00", termin: "2026-03-27 23:59:59")))
            .Should().Be(D("2026-03-27 23:59:59"));
        Calc().AktualniDeadline(Pmp(
                U(TransferTyp.U1, "2026-03-20 10:00", termin: "2026-03-27 23:59:59"),
                U(TransferTyp.U11, "2026-03-21 10:00", termin: "2026-04-10 23:59:59")))
            .Should().Be(D("2026-04-10 23:59:59"));
    }

    [Fact]
    public void AktualniDeadline_vraceny_ticket_nema_deadline()
        => Calc().AktualniDeadline(Pmp(
                U(TransferTyp.U1, "2026-03-20 10:00", termin: "2026-03-27 23:59:59"),
                U(TransferTyp.U3, "2026-03-21 10:00")))
            .Should().BeNull();
}
