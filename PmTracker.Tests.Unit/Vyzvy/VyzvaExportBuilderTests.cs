using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Projekce výzvy pro tisk (spec 2026-09-07 §9.3–9.5). Word i PDF staví z tohoto modelu,
/// takže co se ověří tady, platí pro oba formáty.
/// </summary>
public sealed class VyzvaExportBuilderTests
{
    private const int VyzvaId = 10;

    /// <summary>Ticketing, který vrací připravené HOT popisy a kalkulace podle čísla PNF.</summary>
    private sealed class FakeTicketing : ITicketingQueryService
    {
        public Dictionary<string, HotZaznamDto> Zaznamy { get; } = new();
        public Dictionary<string, HotKalkulaceDto> Kalkulace { get; } = new();

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult(Zaznamy.GetValueOrDefault(cislo));

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotZaznamDto>>(
                cisla.Where(Zaznamy.ContainsKey).ToDictionary(c => c, c => Zaznamy[c]));

        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string pid, CancellationToken ct)
            => Task.FromResult(Kalkulace.GetValueOrDefault(pid));

        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> pidy, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(
                pidy.Where(Kalkulace.ContainsKey).ToDictionary(p => p, p => Kalkulace[p]));
    }

    private static HotKalkulaceDto Kalkulace(string pid) => new(
        1, pid,
        PracnostAnalyza: 2m, SazbaAnalyza: 100m, CenaAnalyza: 200m,
        PracnostProgramovani: 3m, SazbaProgramovani: 200m, CenaProgramovani: 600m,
        PracnostTestovani: 1m, SazbaTestovani: 50m, CenaTestovani: 50m,
        PracnostImplementace: 1m, SazbaImplementace: 150m, CenaImplementace: 150m,
        CenaCelkem: 1000m,
        PocetLicenci: null, SazbaLicence: null, CenaLicence: null, RozpadLicence: null,
        TextTermin: null);

    private static async Task<PmTracker.Web.Data.PmTrackerDbContext> SeedAsync(
        params (int ZaznamId, string Cislo)[] pnf)
    {
        var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = VyzvaId, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 2, 6), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "FIS (EIS): VZ 8201", CisloRamcoveSmlouvySnapshot = "23106000271",
        });

        var id = 500;
        foreach (var (zaznamId, cislo) in pnf)
        {
            await VyzvaServiceTestHarness.SeedZaznamAsync(db, zaznamId);
            db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                Id = id++, ZaznamId = zaznamId, TypOdkazuId = 1, Cislo = cislo,
                ZaradidDoVyzvy = true, VyzvaId = VyzvaId,
            });
        }
        await db.SaveChangesAsync();
        return db;
    }

    private static VyzvaExportBuilder Builder(
        PmTracker.Web.Data.PmTrackerDbContext db, ITicketingQueryService ticketing)
        => new(db, ticketing, new PmTracker.Web.Services.Common.RichTextContentService());

    [Fact]
    public async Task Build_HlavickaZeSnapshotuVyzvy()
    {
        using var db = await SeedAsync((100, "336865"));

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model.Should().NotBeNull();
        model!.KodVyzvy.Should().Be("2/2026");
        model.CisloRamcoveSmlouvy.Should().Be("23106000271");
        model.MistoPlneni.Should().Be("FIS (EIS): VZ 8201");
        model.InformacniSystem.Should().Be("FIS", "nadpis nese zkratku IS z místa plnění");
    }

    [Fact]
    public async Task Build_VyzvaVPriprave_MistoPlneniZAktualnihoProjektu()
    {
        using var db = await SeedAsync();
        var projekt = await db.Projekty.FindAsync(1);
        projekt!.MistoPlneni = "EIS (FIS): VZ 9999, Nová 2, 110 00 Praha 1";
        await db.SaveChangesAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model!.MistoPlneni.Should().Be("EIS (FIS): VZ 9999, Nová 2, 110 00 Praha 1",
            "výzva v Přípravě se řídí projektem, i když se změnil po jejím založení");
        model.InformacniSystem.Should().Be("EIS");
    }

    [Theory]
    [InlineData(VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno)]
    public async Task Build_VyzvaMimoPripravu_MistoPlneniZUlozeneHodnoty(VyzvaStav stav)
    {
        using var db = await SeedAsync();
        (await db.Projekty.FindAsync(1))!.MistoPlneni = "EIS (FIS): VZ 9999";
        (await db.Vyzvy.FindAsync(VyzvaId))!.Stav = stav;
        await db.SaveChangesAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model!.MistoPlneni.Should().Be("FIS (EIS): VZ 8201",
            "odeslaná výzva se tiskne tak, jak šla dodavateli");
        model.InformacniSystem.Should().Be("FIS");
    }

    [Fact]
    public async Task Build_ProjektBezMistaPlneni_PouzijeUlozenouHodnotu()
    {
        using var db = await SeedAsync();
        (await db.Projekty.FindAsync(1))!.MistoPlneni = null;
        await db.SaveChangesAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model!.MistoPlneni.Should().Be("FIS (EIS): VZ 8201");
    }

    [Fact]
    public async Task Build_PozadavkyMajiRimskaPoradovaCislaAUdajeZeZaznamu()
    {
        using var db = await SeedAsync((100, "336865"), (200, "341837"));

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model!.Pozadavky.Select(p => p.PoradoveOznaceni).Should().Equal(new[] { "I.", "II." });
        var prvni = model.Pozadavky[0];
        prvni.CisloUkoluVp.Should().Be("RU100", "Č. úkolu VP je viditelné číslo záznamu");
        prvni.Nazev.Should().Be("test");
        prvni.CisloHtl.Should().Be("336865");
    }

    [Fact]
    public async Task Build_KalkulaceMaCtyriRadkyAPocitaDph()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "strucne", "popis", Pid: "A490P00ECYL1");
        ticketing.Kalkulace["A490P00ECYL1"] = Kalkulace("A490P00ECYL1");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        var k = model!.Pozadavky[0].Kalkulace;
        k.Should().NotBeNull();
        k!.Radky.Select(r => r.Kod).Should().Equal(new[] { "A", "B", "C", "D" });
        k.Radky.Select(r => r.Nazev).Should().Equal(
            new[] { "Analýza", "Programové úpravy", "Testování", "Implementace" });
        k.Radky[1].Rozsah.Should().Be(3m);
        k.Radky[1].Sazba.Should().Be(200m);
        k.Radky[1].CenaBezDph.Should().Be(600m);

        k.CelkemBezDph.Should().Be(1000m, "součet cen A–D, v databázi jsou bez DPH");
        k.CelkemDph.Should().Be(210m, "DPH 21 %");
        k.CelkemSDph.Should().Be(1210m);
    }

    [Fact]
    public async Task Build_PnfBezKalkulace_MaPrazdneRadkyNeNull()
    {
        using var db = await SeedAsync((100, "336865"));

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        var k = model!.Pozadavky[0].Kalkulace;
        k.Should().NotBeNull("tabulka se tiskne i bez dat, jen prázdná");
        k!.Radky.Should().HaveCount(4);
        k.Radky.Should().OnlyContain(r => r.Rozsah == null && r.CenaBezDph == null);
        k.CelkemBezDph.Should().Be(0m);
        k.MaCinnosti.Should().BeFalse("bez akceptované kalkulace se netiskne žádná tabulka");
        k.MaLicenci.Should().BeFalse();
    }

    [Fact]
    public async Task Build_PrazdnaVyzva_SePorenderujeBezPozadavku()
    {
        using var db = await SeedAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model.Should().NotBeNull("prázdná výzva se tiskne (spec §9.7)");
        model!.Pozadavky.Should().BeEmpty();
        model.CelkemBezDph.Should().Be(0m);
    }

    [Fact]
    public async Task Build_NeexistujiciVyzva_VraciNull()
    {
        using var db = await SeedAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(vyzvaId: 999, CancellationToken.None);

        model.Should().BeNull();
    }

    /// <summary>
    /// Text do výzvy jde z externí vazby, ne z popisu tiketu. Celý smysl změny 2026-09-08
    /// je, aby v dokumentu byl jen text, který pracovník napsal a viděl.
    /// </summary>
    [Fact]
    public async Task Build_BereTextZVazby_NeZTiketu()
    {
        using var db = await SeedAsync((100, "336865"));
        db.ZaznamExterniOdkazy.Single(x => x.Cislo == "336865").Pozadavek =
            "<p>Chceme sestavu.</p>";
        await db.SaveChangesAsync();

        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto(
            "336865", "PNF", "strucne", "Popis z HOT");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        var pozadavek = model!.Pozadavky.Should().ContainSingle().Which;
        pozadavek.PozadavekHtml.Should().Contain("Chceme sestavu.");
        pozadavek.PozadavekHtml.Should().NotContain("Popis z HOT",
            "popis tiketu se do výzvy už nedostane");
    }

    /// <summary>
    /// Vazby založené před 2026-09-08 mají NULL a popis tiketu se jako náhrada nepoužívá
    /// (spec R2 + R3). U požadavku se tedy nevytiskne nic, dokud pracovník text nevyplní.
    /// </summary>
    [Fact]
    public async Task Build_PrazdnyPozadavek_NevraciPopisTiketu()
    {
        using var db = await SeedAsync((100, "336865"));

        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto(
            "336865", "PNF", "strucne", "Popis z HOT");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        var pozadavek = model!.Pozadavky.Should().ContainSingle().Which;
        pozadavek.PozadavekHtml.Should().BeNullOrWhiteSpace(
            "prázdná hodnota znamená ve výzvě žádný text, ne náhradu z tiketu");
    }

    /// <summary>
    /// Dokument je výstup ven, takže sanitizace nesmí záviset jen na tom, že do databáze
    /// nikdy nic nepřišlo jinudy než přes uložení záznamu (spec §8, řádek „Unit — sanitizace").
    /// </summary>
    [Fact]
    public async Task Build_SkriptSeDoDokumentuNedostane()
    {
        using var db = await SeedAsync((100, "336865"));
        db.ZaznamExterniOdkazy.Single(x => x.Cislo == "336865").Pozadavek =
            "<p>Text</p><script>alert(1)</script>";
        await db.SaveChangesAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        var html = model!.Pozadavky.Single().PozadavekHtml;
        html.Should().Contain("Text");
        html.Should().NotContain("<script", "sanitizace běží i při stavbě dokumentu");

        // Sanitizér jede na allowlistu: nepovolenou značku zahodí, ale její text zachová
        // a zakóduje — nikdy neztratí, co uživatel napsal. Zbylé "alert(1)" je proto
        // neškodný prostý text, ne spustitelný kód. Stejně se chová popis záznamu.
        html.Should().Contain("alert(1)", "text nepovolené značky se zachová jako prostý text");
        html.Should().NotContain("</script", "zavírací značka taky mizí");
    }

    [Theory]
    [InlineData(0, "I.")]
    [InlineData(3, "IV.")]
    [InlineData(8, "IX.")]
    [InlineData(17, "XVIII.")]
    [InlineData(39, "XL.")]
    public void PoradoveOznaceni_JeRimskySTeckou(int index, string ocekavane)
    {
        VyzvaExportBuilder.PoradoveOznaceni(index).Should().Be(ocekavane);
    }

    [Theory]
    [InlineData("867-5", "RU867-5")]
    [InlineData("123", "RU123")]
    [InlineData("RU867-5", "RU867-5")]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void CisloUkoluVp_DostanePrefixRUBezZdvojeni(string? cisloViditelne, string? ocekavane)
    {
        VyzvaExportBuilder.CisloUkoluVp(cisloViditelne).Should().Be(ocekavane);
    }

    private static HotKalkulaceDto KalkulaceJenLicence(string pid, decimal cenaL, string? rozpad) => new(
        5414, pid,
        PracnostAnalyza: 0m, SazbaAnalyza: 0m, CenaAnalyza: 0m,
        PracnostProgramovani: 0m, SazbaProgramovani: 0m, CenaProgramovani: 0m,
        PracnostTestovani: 0m, SazbaTestovani: 0m, CenaTestovani: 0m,
        PracnostImplementace: 0m, SazbaImplementace: 0m, CenaImplementace: 0m,
        CenaCelkem: cenaL,
        PocetLicenci: 1m, SazbaLicence: cenaL, CenaLicence: cenaL, RozpadLicence: rozpad,
        TextTermin: null);

    /// <summary>Čistě licenční kalkulace: tabulka licencí ano, tabulka činností ne (spec B5).</summary>
    [Fact]
    public async Task Build_JenLicence_RadkyZRozpadu()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "s", "p", Pid: "P1");
        ticketing.Kalkulace["P1"] = KalkulaceJenLicence("P1", 188940m,
            "<table><tr><td>XRG – RSS</td><td>94470,00</td></tr><tr><td>XRG - ESS</td><td>94470,00</td></tr></table>");

        var k = (await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None))!
            .Pozadavky.Single().Kalkulace;

        k.MaCinnosti.Should().BeFalse();
        k.MaLicenci.Should().BeTrue();
        k.LicenceRadky.Select(r => r.Kod).Should().Equal(1, 2);
        k.LicenceRadky.Select(r => r.Nazev).Should().Equal("XRG – RSS", "XRG - ESS");
        k.LicenceRadky[0].CenaDph.Should().Be(19838.70m, "DPH 21 %");
        k.CenaLicence.Should().Be(188940m);
    }

    /// <summary>Bez čitelného rozpadu jeden řádek s cena_l (spec B5).</summary>
    [Fact]
    public async Task Build_LicenceBezRozpadu_JedenRadekZCenyL()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "s", "p", Pid: "P1");
        ticketing.Kalkulace["P1"] = KalkulaceJenLicence("P1", 10340m, rozpad: null);

        var radek = (await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None))!
            .Pozadavky.Single().Kalkulace.LicenceRadky.Should().ContainSingle().Which;

        radek.Nazev.Should().Be("Licenční rozšíření");
        radek.CenaBezDph.Should().Be(10340m);
    }

    [Fact]
    public async Task Build_CinnostiBezLicence_MaJenCinnosti()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "s", "p", Pid: "P1");
        ticketing.Kalkulace["P1"] = Kalkulace("P1");

        var k = (await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None))!
            .Pozadavky.Single().Kalkulace;

        k.MaCinnosti.Should().BeTrue();
        k.MaLicenci.Should().BeFalse();
        k.LicenceRadky.Should().BeEmpty();
    }
}
