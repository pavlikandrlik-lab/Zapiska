using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramDateBlokBuilderTests
{
    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly DateTime Today = new(2026, 2, 1);

    private static ZaznamHarmonogramKrokEntity Row(int poradi, DateTime? plan, DateTime? skut)
        => new() { Poradi = (byte)poradi, PlanDatum = plan, SkutecnostDatum = skut };

    /// <summary>
    /// 2026-09-03 (bug): read-only zobrazení (karta, tabulka na stránce záznamu) psalo
    /// „Automat zatím nenašel vhodné vyjádření", i když skutečnost z vyjádření existovala
    /// a editor ji ukazoval. Builder plnil surový SkutecnostZdroj z DB, ale nemapoval ho
    /// na ZdrojSkutecnosti, podle kterého se řídí vykreslení buňky → zůstalo None.
    /// </summary>
    [Fact]
    public void BuildKroky_krok_vytezeny_automatem_ma_ZdrojSkutecnosti_FromVyjadreni()
    {
        var row = Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 13));
        row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, new[] { row }, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.ZdrojSkutecnosti.Should().Be(ZdrojSkutecnosti.FromVyjadreni,
            "skutečnost vytěženou automatem má read-only buňka zobrazit jako datum, ne jako varování");
        k1.SkutecneDatum.Should().Be(new DateTime(2026, 1, 13));
    }

    [Fact]
    public void BuildKroky_rucne_zadana_skutecnost_ma_ZdrojSkutecnosti_Manual()
    {
        var row = Row(2, new DateTime(2026, 1, 17), new DateTime(2026, 1, 18));
        row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual;

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, new[] { row }, Today);

        kroky.Single(k => k.KrokIndex == 2).ZdrojSkutecnosti.Should().Be(ZdrojSkutecnosti.Manual);
    }

    [Fact]
    public void BuildKroky_krok_bez_skutecnosti_zustava_None()
    {
        var kroky = HarmonogramDateBlokBuilder.BuildKroky(
            Start, new[] { Row(3, new DateTime(2026, 1, 24), null) }, Today);

        kroky.Single(k => k.KrokIndex == 3).ZdrojSkutecnosti.Should().Be(ZdrojSkutecnosti.None,
            "bez data skutečnosti zůstává varování na místě");
    }

    [Fact]
    public void BuildKroky_nezapina_ovladani_zdroje_skutecnosti()
    {
        // Read-only zobrazení nesmí nabídnout výběr jiného ticketu ani přepínání režimu —
        // ta afordance patří jen editoru (composition ji povoluje dle records.schedule.edit).
        var row = Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 13));
        row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, new[] { row }, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.CanToggleRezim.Should().BeFalse("read-only nesmí přepínat Auto/Ručně");
        k1.Kandidati.Should().BeEmpty("read-only nenabízí výběr jiného zdrojového ticketu");
    }

    [Fact]
    public void BuildKroky_vraci_10_kroku_s_nazvy_z_konstanty()
    {
        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, Array.Empty<ZaznamHarmonogramKrokEntity>(), Today);

        kroky.Should().HaveCount(10);
        kroky[0].KrokIndex.Should().Be(1);
        kroky[0].Nazev.Should().Be("1. priprava zadani dodavateli");
    }

    [Fact]
    public void BuildKroky_vyplneny_krok_ma_odchylku_jako_skut_minus_plan()
    {
        var rows = new[]
        {
            Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 13)), // +3 dny
        };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.BaselineDatum.Should().Be(new DateTime(2026, 1, 10));
        k1.SkutecneDatum.Should().Be(new DateTime(2026, 1, 13));
        k1.OdchylkaDni.Should().Be(3);
        k1.Stav.Should().Be(HarmonogramKrokStav.Splneno);
    }

    [Fact]
    public void BuildKroky_nevyplneny_krok_ma_null_SkutecneDatum_zadny_PlanEnd_fallback()
    {
        var rows = new[] { Row(1, new DateTime(2026, 1, 10), null) };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.OdchylkaDni.Should().BeNull();
        k1.SkutecneDatum.Should().BeNull("zrušen PlanEnd fallback (zdroj zkreslení skutečnost=plán)");
        k1.Stav.Should().Be(HarmonogramKrokStav.VProdleni, "plán 10.1. < dnes 1.2. a nevyplněno");
    }

    [Fact]
    public void BuildKroky_exposesPlanAndActualStartEnd_ForTooltip()
    {
        // krok 1 vyplněn (skut 13.1.), krok 2 nevyplněn (aktuální → táhne do dneška).
        var rows = new[]
        {
            Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 13)),
            Row(2, new DateTime(2026, 1, 20), null),
        };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.PlanZacatek.Should().Be(new DateTime(2026, 1, 1), "plán kroku 1 začíná datem založení");
        k1.BaselineDatum.Should().Be(new DateTime(2026, 1, 10));
        k1.SkutecnostZacatek.Should().Be(new DateTime(2026, 1, 1));
        k1.SkutecnostKonec.Should().Be(new DateTime(2026, 1, 13));

        var k2 = kroky.Single(k => k.KrokIndex == 2);
        k2.PlanZacatek.Should().Be(new DateTime(2026, 1, 10), "plán kroku 2 navazuje na konec kroku 1");
        k2.SkutecnostKonec.Should().Be(Today, "aktuální (nevyplněný) krok táhne skutečnost do dneška");

        var k3 = kroky.Single(k => k.KrokIndex == 3);
        k3.SkutecnostZacatek.Should().BeNull("krok 3 nemá skutečnost (není vyplněný ani aktuální)");
    }

    [Fact]
    public void BuildKroky_PlanDatum_je_surove_null_u_nevyplnenych_hodnota_u_vyplnenych()
    {
        // Krok 3 má plán, kroky 1 a 2 ne. Surové PlanDatum musí být null tam, kde plán neexistuje —
        // NE dopočtený BaselineDatum (který pro nevyplněné kolabuje na datum založení). Jinak by
        // editor pre-filloval prázdná pole datem založení a vynucoval kaskádu překlikávání.
        var rows = new[] { Row(3, new DateTime(2026, 1, 20), null) };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows, Today);

        var k3 = kroky.Single(k => k.KrokIndex == 3);
        k3.PlanDatum.Should().Be(new DateTime(2026, 1, 20), "surové uložené plánové datum vyplněného kroku");

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.PlanDatum.Should().BeNull("nevyplněný krok nesmí mít pre-fill v editoru");
        k1.BaselineDatum.Should().Be(Start, "BaselineDatum (dopočtený) dál kolabuje na začátek pro bar/tooltip");
    }

    [Fact]
    public void BuildKroky_bezRadku_ma_vsechna_PlanDatum_null()
    {
        // Create flow (nový záznam) — žádné krok řádky → všechna surová plánová data null (prázdné pole).
        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, Array.Empty<ZaznamHarmonogramKrokEntity>(), Today);

        kroky.Should().OnlyContain(k => k.PlanDatum == null);
        kroky.Should().OnlyContain(k => k.BaselineDatum == Start, "bez plánu bar kolabuje na datum založení");
    }

    [Fact]
    public void BuildSouhrn_ma_aktualni_krok_a_znamenkove_prekroceni()
    {
        // krok 1 vyplněn, krok 2 (a dál) nevyplněn → aktuální = 2
        var rows = new[]
        {
            Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 11)),
            Row(2, new DateTime(2026, 1, 20), null),
        };

        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
            Start, rows, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 2, 1));

        souhrn.AktualniKrokPoradi.Should().Be(2);
        souhrn.AktualniKrokNazev.Should().NotBeNullOrEmpty();
        souhrn.PrekroceniDni.Should().Be((new DateTime(2026, 2, 1) - new DateTime(2026, 1, 20)).Days);
        souhrn.Dokonceno.Should().BeFalse();
    }

    [Fact]
    public void BuildSouhrn_vse_vyplnene_je_dokonceno()
    {
        var rows = Enumerable.Range(1, 10)
            .Select(i => Row(i, new DateTime(2026, 1, 1).AddDays(i), new DateTime(2026, 1, 1).AddDays(i)))
            .ToArray();

        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
            Start, rows, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 2, 1));

        souhrn.Dokonceno.Should().BeTrue();
        souhrn.AktualniKrokPoradi.Should().BeNull();
        souhrn.PrekroceniDni.Should().Be(0);
    }
}
