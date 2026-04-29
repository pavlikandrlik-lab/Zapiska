using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Pure-logic testy pro <see cref="PerTicketMetadataExtractor"/>.
/// Spec: 2026-04-28-nes-vyjadreni-a-4-datumy-design §4.
/// </summary>
public sealed class PerTicketMetadataExtractorTests
{
    private static HotVyjadreniDto V(long id, string datum, string popis)
        => new(id, "25", "PID-1", DateTime.Parse(datum), "uzivatel", popis, "Tym", 1);

    // -------- NES --------

    [Fact]
    public void Extract_Nes_HappyPath_VratiVsechny4Datumy()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Záznam byl předán dodavateli k řešení."),
            V(2, "2026-01-15", "Nějaké zpracování."),
            V(3, "2026-02-01", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 123456 byla vytvořena."),
            V(4, "2026-03-01", "Záznam byl převeden do archivu."),
        };

        var sla = new DateTime(2026, 2, 28, 12, 0, 0);
        var meta = PerTicketMetadataExtractor.Extract("NES", sla, all);

        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 1));
        meta.PlanDodani.Should().Be(new DateTime(2026, 2, 28));   // .Date
        meta.DatumDodani.Should().Be(new DateTime(2026, 2, 1));
        meta.DatumPrevzeti.Should().Be(new DateTime(2026, 3, 1));
    }

    [Fact]
    public void Extract_Nes_VicNesDodani_VratiPosledniDesc()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 100001 vytvořena."),
            V(2, "2026-02-01", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 100002 vytvořena."),
        };

        var meta = PerTicketMetadataExtractor.Extract("NES", null, all);

        meta.DatumDodani.Should().Be(new DateTime(2026, 2, 1));   // poslední (DESC)
        meta.PlanDodani.Should().BeNull();                        // sla_deadline je null
        meta.DatumObjednani.Should().BeNull();
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_Nes_BezVyjadreni_VsechnyNullKromeSla()
    {
        var sla = new DateTime(2026, 6, 30);
        var meta = PerTicketMetadataExtractor.Extract("NES", sla, Array.Empty<HotVyjadreniDto>());

        meta.PlanDodani.Should().Be(new DateTime(2026, 6, 30));
        meta.DatumObjednani.Should().BeNull();
        meta.DatumDodani.Should().BeNull();
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_Nes_VicObjednani_VratiPrvni()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Záznam byl předán dodavateli k řešení."),
            V(2, "2026-02-01", "Záznam byl předán dodavateli k řešení."),
        };

        var meta = PerTicketMetadataExtractor.Extract("NES", null, all);
        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 1));
    }

    // -------- PMP --------

    [Fact]
    public void Extract_Pmp_HappyPath_VratiVsechny4Datumy()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
            V(3, "2026-02-01", "Dodavatel přidal řešení."),
            V(4, "2026-02-15", "Dodavatel přidal řešení."),
            V(5, "2026-03-01", "Záznam byl převeden do archivu."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);

        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 2));
        meta.PlanDodani.Should().Be(new DateTime(2026, 4, 15));
        meta.DatumDodani.Should().Be(new DateTime(2026, 2, 15));
        meta.DatumPrevzeti.Should().Be(new DateTime(2026, 3, 1));
    }

    [Fact]
    public void Extract_Pmp_PlanDodaniBezNavazujiciKalkulace_VratiNull()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, "2026-01-02", "Něco úplně jiného."),
            V(3, "2026-01-03", "A další něco jiného."),
            V(4, "2026-01-04", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().BeNull();   // K6 v n+3 — moc daleko
    }

    [Fact]
    public void Extract_Pmp_PlanDodaniKalkulaceVNplus2_ValidaceProjde()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, "2026-01-02", "Mezikrok."),
            V(3, "2026-01-03", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().Be(new DateTime(2026, 4, 15));
    }

    [Fact]
    public void Extract_Pmp_NeplatneDateMimoRozsah_VratiNull()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 32.13.2026"),
            V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().BeNull();
    }

    [Fact]
    public void Extract_Pmp_PlanDodaniDateImmediatelyAfterDodavatele_NotLastDateInText()
    {
        // Bug 2026-04-29: real-world PlanDodani vyjádření může v dalších větách
        // obsahovat další datum (např. "Záznam byl převzat dne 20.5.2026"). Regex
        // musí vzít datum IHNED po slově "dodavatele" v "s termínem plnění dodavatele",
        // NE poslední datum v textu (které by chytlo jiný kontext).
        var all = new[]
        {
            V(1, "2026-01-01",
                "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026. " +
                "Záznam byl převzat k řešení dne 20.5.2026."),
            V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };
        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().Be(new DateTime(2026, 4, 15));   // datum těsně po "dodavatele"
        // PŘED FIX: TryParseLastDateInText by vrátil 20.5.2026 (poslední match v textu)
    }

    [Fact]
    public void Extract_Pmp_HtmlWrappersOkolu_StaleParsuje()
    {
        var all = new[]
        {
            V(1, "2026-01-01",
                "<br>VS FIS předal záznam dodavateli : <b>DODAVATEL</b> s termínem plnění dodavatele <b>31.03.2026</b>"),
            V(2, "2026-01-02",
                "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);
        meta.PlanDodani.Should().Be(new DateTime(2026, 3, 31));
    }

    // -------- PNF --------

    [Fact]
    public void Extract_Pnf_HappyPath_StejnaPravidlaJakoPmp()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 30.5.2026"),
            V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
            V(3, "2026-04-01", "Dodavatel přidal řešení."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PNF", null, all);

        meta.DatumObjednani.Should().Be(new DateTime(2026, 1, 2));
        meta.PlanDodani.Should().Be(new DateTime(2026, 5, 30));
        meta.DatumDodani.Should().Be(new DateTime(2026, 4, 1));
        meta.DatumPrevzeti.Should().BeNull();
    }

    // -------- ostatní typy --------

    [Fact]
    public void Extract_NeznamyTyp_VratiVsechnyNull()
    {
        var meta = PerTicketMetadataExtractor.Extract("XYZ", null, Array.Empty<HotVyjadreniDto>());

        meta.DatumObjednani.Should().BeNull();
        meta.PlanDodani.Should().BeNull();
        meta.DatumDodani.Should().BeNull();
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_PrazdnyTyp_VratiVsechnyNull()
    {
        var meta = PerTicketMetadataExtractor.Extract("", null, Array.Empty<HotVyjadreniDto>());
        meta.DatumObjednani.Should().BeNull();
    }

    [Fact]
    public void Extract_NullToleranceJednotlivychPoli()
    {
        // Jen K4/K7 fráze — ostatní 3 datumy null.
        var all = new[]
        {
            V(1, "2026-01-01", "Dodavatel přidal řešení."),
        };

        var meta = PerTicketMetadataExtractor.Extract("PMP", null, all);

        meta.DatumObjednani.Should().BeNull();
        meta.PlanDodani.Should().BeNull();
        meta.DatumDodani.Should().Be(new DateTime(2026, 1, 1));
        meta.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public void Extract_CaseInsensitiveTypZaznamu()
    {
        var all = new[]
        {
            V(1, "2026-01-01", "Záznam byl převeden do archivu."),
        };

        PerTicketMetadataExtractor.Extract("nes", null, all).DatumPrevzeti.Should().Be(new DateTime(2026, 1, 1));
        PerTicketMetadataExtractor.Extract("Pmp", null, all).DatumPrevzeti.Should().Be(new DateTime(2026, 1, 1));
        PerTicketMetadataExtractor.Extract("PNF", null, all).DatumPrevzeti.Should().Be(new DateTime(2026, 1, 1));
    }
}
