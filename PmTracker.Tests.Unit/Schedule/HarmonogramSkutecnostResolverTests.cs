using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Plán 4 Feature C Task 3 — pure logic resolver.
/// MAX default + seznam kandidátů + volitelný <c>PreferredExterniOdkazId</c>.
/// </summary>
public sealed class HarmonogramSkutecnostResolverTests
{
    private static readonly DateTime D1 = new(2026, 3, 1);
    private static readonly DateTime D2 = new(2026, 3, 15);
    private static readonly DateTime D3 = new(2026, 3, 28);

    [Fact]
    public void Resolve_ZadneKandidati_VraciDatumNull()
    {
        var r = HarmonogramSkutecnostResolver.Resolve(3, Array.Empty<BindingKandidat>(), preferredExterniOdkazId: null);

        r.Datum.Should().BeNull();
        r.VybranyExterniOdkazId.Should().BeNull();
        r.Kandidati.Should().BeEmpty();
        r.PreferredFallbackApplied.Should().BeFalse();
    }

    [Fact]
    public void Resolve_JedenKandidat_VraciJeho()
    {
        var b = new BindingKandidat(100, "111111", "PMP", "K4_K7", D2);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b }, preferredExterniOdkazId: null);

        r.Datum.Should().Be(D2);
        r.VybranyExterniOdkazId.Should().Be(100);
        r.Kandidati.Should().HaveCount(1);
    }

    [Fact]
    public void Resolve_DvaKandidati_VraciMaxJakoDefault()
    {
        var b1 = new BindingKandidat(100, "111111", "PMP", "K4_K7", D2);
        var b2 = new BindingKandidat(101, "222222", "PMP", "K4_K7", D3);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b1, b2 }, preferredExterniOdkazId: null);

        r.Datum.Should().Be(D3); // MAX
        r.VybranyExterniOdkazId.Should().Be(101);
        r.Kandidati.Should().HaveCount(2);
        r.Kandidati[0].ExterniOdkazId.Should().Be(101); // MAX first (OrderByDescending)
        r.Kandidati[1].ExterniOdkazId.Should().Be(100);
    }

    [Fact]
    public void Resolve_BindingSPredikatemNesedicimNaKrok_Ignoruje()
    {
        // PMP krok 4 = K4_K7. Binding s K10 je pro jiný krok → neměl by patřit do kandidátů K4.
        var b = new BindingKandidat(100, "111111", "PMP", "K10", D1);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b }, preferredExterniOdkazId: null);

        r.Datum.Should().BeNull();
        r.Kandidati.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_NESKrok1_NemaMapovani_ZadnyKandidat()
    {
        // NES nemá žádné automatické kroky → matice vrátí null → žádné kandidáty pro žádný krok.
        var b = new BindingKandidat(100, "111111", "NES", "K3", D1);

        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { b }, preferredExterniOdkazId: null);

        r.Datum.Should().BeNull();
        r.Kandidati.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_MixTypu_PouzeMatchingPredikatProDanyKrok()
    {
        // Záznam má 2 externí vazby: 1× PMP (K3) + 1× PNF (K6).
        // Pro PMP krok 3 by měl zůstat pouze PMP K3 binding (PNF nemá K3).
        var pmpBinding = new BindingKandidat(100, "111111", "PMP", "K3", D1);
        var pnfBinding = new BindingKandidat(101, "222222", "PNF", "K6", D2);

        var r = HarmonogramSkutecnostResolver.Resolve(3, new[] { pmpBinding, pnfBinding }, preferredExterniOdkazId: null);

        r.Kandidati.Should().HaveCount(1);
        r.Kandidati[0].ExterniOdkazId.Should().Be(100);
        r.Datum.Should().Be(D1);
    }

    [Fact]
    public void Resolve_PreferredExterniOdkazExistuje_VraciPreferred()
    {
        // User dříve v UI vybral binding 100 (dřívější datum) jako preferred — musí se vrátit on, ne MAX.
        var b1 = new BindingKandidat(100, "111111", "PMP", "K4_K7", D1);
        var b2 = new BindingKandidat(101, "222222", "PMP", "K4_K7", D3);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b1, b2 }, preferredExterniOdkazId: 100);

        r.Datum.Should().Be(D1);
        r.VybranyExterniOdkazId.Should().Be(100);
        r.Kandidati.Should().HaveCount(2);
        r.PreferredFallbackApplied.Should().BeFalse();
    }

    [Fact]
    public void Resolve_PreferredNeexistujeAleJsouKandidati_FallbackNaMax()
    {
        // User vybral binding 999 jako preferred, ale harvest ho odstranil.
        // Musí spadnout zpět na MAX a signalizovat fallback (caller pak clear-uje preferred).
        var b1 = new BindingKandidat(100, "111111", "PMP", "K4_K7", D1);
        var b2 = new BindingKandidat(101, "222222", "PMP", "K4_K7", D3);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b1, b2 }, preferredExterniOdkazId: 999);

        r.Datum.Should().Be(D3); // MAX
        r.VybranyExterniOdkazId.Should().Be(101);
        r.PreferredFallbackApplied.Should().BeTrue();
    }

    [Fact]
    public void Resolve_PreferredNeexistujeAniKandidati_VraciNull()
    {
        var r = HarmonogramSkutecnostResolver.Resolve(4, Array.Empty<BindingKandidat>(), preferredExterniOdkazId: 999);

        r.Datum.Should().BeNull();
        r.VybranyExterniOdkazId.Should().BeNull();
        // Žádní kandidáti → fallback se nezaznamenává, protože není kam fallbackovat.
        r.PreferredFallbackApplied.Should().BeFalse();
    }

    [Fact]
    public void Resolve_NormalizujeTypZaznamuPriMatchingPredikatu()
    {
        // Input binding má typ "pmp" (lowercase) — matice normalizuje na upper case při lookupu.
        var b = new BindingKandidat(100, "111111", "pmp", "K4_K7", D2);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b }, preferredExterniOdkazId: null);

        r.Datum.Should().Be(D2);
    }

    [Fact]
    public void Resolve_PrazdnyTypZaznamuVBindingu_Ignoruje()
    {
        var b = new BindingKandidat(100, "111111", "", "K4_K7", D2);

        var r = HarmonogramSkutecnostResolver.Resolve(4, new[] { b }, preferredExterniOdkazId: null);

        r.Kandidati.Should().BeEmpty();
        r.Datum.Should().BeNull();
    }

    // -------- Krok 1 MIN výjimka (spec 2026-04-28 §2) --------

    [Fact]
    public void Resolve_Krok1_DvaKandidati_VraciMinNejdrivejsi()
    {
        // PMP záznam s 2 napojenými PMP tickety. Krok 1 = K1 (datum založení).
        // Default chování: krok 1 = MIN agregace (nejdřívější datum napříč ticketu).
        var b1 = new BindingKandidat(100, "111111", "PMP", "K1", new DateTime(2026, 1, 1));
        var b2 = new BindingKandidat(101, "222222", "PMP", "K1", new DateTime(2026, 2, 15));

        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { b1, b2 }, preferredExterniOdkazId: null);

        r.Datum.Should().Be(new DateTime(2026, 1, 1));        // MIN
        r.VybranyExterniOdkazId.Should().Be(100);             // První v ASC pořadí
        r.Kandidati[0].ExterniOdkazId.Should().Be(100);       // MIN first
        r.Kandidati[1].ExterniOdkazId.Should().Be(101);
    }

    [Fact]
    public void Resolve_Krok1_MixPmpPnf_VraciMinNapricVsemiTypy()
    {
        // Záznam s PMP i PNF napojením. Oba mají K1 mapování. MIN přes všechny.
        var pmp = new BindingKandidat(100, "111111", "PMP", "K1", new DateTime(2026, 3, 1));
        var pnf = new BindingKandidat(101, "222222", "PNF", "K1", new DateTime(2026, 1, 15));

        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { pmp, pnf }, preferredExterniOdkazId: null);

        r.Datum.Should().Be(new DateTime(2026, 1, 15));       // PNF dřívější
        r.VybranyExterniOdkazId.Should().Be(101);
    }

    [Fact]
    public void Resolve_Krok3_DvaKandidati_VraciMaxJakoDefault()
    {
        // Sanity: ostatní kroky (3, 4, 6, 7, 10) zůstávají MAX.
        var b1 = new BindingKandidat(100, "111111", "PMP", "K3", new DateTime(2026, 1, 1));
        var b2 = new BindingKandidat(101, "222222", "PMP", "K3", new DateTime(2026, 2, 15));

        var r = HarmonogramSkutecnostResolver.Resolve(3, new[] { b1, b2 }, preferredExterniOdkazId: null);

        r.Datum.Should().Be(new DateTime(2026, 2, 15));       // MAX (default)
        r.VybranyExterniOdkazId.Should().Be(101);
    }

    /// <summary>
    /// FIX 2026-05-04: krok 1 = MIN agregace, preferred koncepce neplatí.
    /// Spec 2026-04-28 §2 — „nejdřívější datum založení tiketu" je deterministická agregace
    /// napříč externími vazbami, nikoli volba uživatele. Předchozí chování (preferred přebil
    /// MIN) způsobovalo bug: user původně měl jen PMP → sync uložil preferred=PMP. Po přidání
    /// PNF s dřívějším HOT_ZAZNAMY.datum resolver stále vracel PMP datum místo PNF (= MIN).
    /// </summary>
    [Fact]
    public void Resolve_Krok1_PreferredJeIgnorovan_VzdyVraciMin()
    {
        var b1 = new BindingKandidat(100, "111111", "PMP", "K1", new DateTime(2026, 1, 1));
        var b2 = new BindingKandidat(101, "222222", "PMP", "K1", new DateTime(2026, 2, 15));

        // Preferred=101 (MAX), ale resolver pro krok 1 ho ignoruje.
        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { b1, b2 }, preferredExterniOdkazId: 101);

        r.Datum.Should().Be(new DateTime(2026, 1, 1));        // MIN, preferred ignorován
        r.VybranyExterniOdkazId.Should().Be(100);
        r.PreferredFallbackApplied.Should().BeTrue(
            "preferred=101 ≠ MIN winner=100 — sync má clear-nout stale preferred z DELAY row");
    }

    [Fact]
    public void Resolve_Krok1_PreferredEqualsMin_NoFallbackSignal()
    {
        // Když je preferred = MIN (např. po předchozím sync clear), žádný fallback signál.
        var b1 = new BindingKandidat(100, "111111", "PMP", "K1", new DateTime(2026, 1, 1));
        var b2 = new BindingKandidat(101, "222222", "PMP", "K1", new DateTime(2026, 2, 15));

        var r = HarmonogramSkutecnostResolver.Resolve(1, new[] { b1, b2 }, preferredExterniOdkazId: 100);

        r.Datum.Should().Be(new DateTime(2026, 1, 1));
        r.VybranyExterniOdkazId.Should().Be(100);
        r.PreferredFallbackApplied.Should().BeFalse();
    }
}
