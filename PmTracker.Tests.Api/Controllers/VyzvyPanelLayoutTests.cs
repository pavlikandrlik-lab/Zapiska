using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Rozvržení panelu Výzev po grafickém předělání 2026-09-08 (zadání uživatele):
/// lišta vpravo nahoře nese výběr roku, „Uzavřít výzvu" a „Nová výzva"; tisk se stěhoval
/// do patičky panelu a je jen u výzvy; dlaždice výzvy je dvouřádková a PNF je karta.
///
/// Assertace se kotví na atributy a ASCII — Razor kóduje diakritiku na číselné entity.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyPanelLayoutTests
{
    private const string Marker = "APIVYZLAYOUT";
    private const string Smlouva = "APIVYZ-SML-LAYOUT";

    private readonly ApiSqlFixture _fixture;

    public VyzvyPanelLayoutTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int PripravaId, int OdeslanoId);

    /// <summary>
    /// Seed je idempotentní — Api testy sdílí jednu databázi, takže se výzvy zakládají
    /// jen když ještě nejsou. Čísla jsou unikátní kvůli globálnímu indexu
    /// ux_vyzvy_smlouva_rok_poradove (není per projekt).
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        var projectId = await _fixture.EnsureProjectAsync(Marker);
        var ownerId = await _fixture.EnsurePersonAsync("ApiVyzvyLayoutOwner");

        await using var db = _fixture.CreateDbContext();

        var projekt = await db.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = Smlouva;
        await db.SaveChangesAsync();

        var priprava = await EnsureVyzvaAsync(db, projectId, ownerId, 401, VyzvaStav.Priprava);
        var odeslano = await EnsureVyzvaAsync(db, projectId, ownerId, 402, VyzvaStav.Odeslano);

        if (!await db.ZaznamExterniOdkazy.AnyAsync(x => x.Cislo == "941001"))
        {
            var subsystemId = await _fixture.EnsureSubsystemAsync(Marker + "_SUB", ownerId);
            var recordId = await db.ProjektoveZaznamy.Where(z => z.Nazev == "Zaznam pro layout")
                .Select(z => (int?)z.Id).FirstOrDefaultAsync()
                ?? await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Zaznam pro layout");

            var pnfTypeId = await db.CiselnikTypuExternichOdkazu.AsNoTracking()
                .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();

            db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = pnfTypeId,
                Cislo = "941001",
                ZaradidDoVyzvy = true,
                VyzvaId = priprava,
            });
            await db.SaveChangesAsync();
        }

        return new Seed(projectId, priprava, odeslano);
    }

    private static async Task<int> EnsureVyzvaAsync(
        PmTracker.Web.Data.PmTrackerDbContext db, int projectId, int ownerId, int poradove, VyzvaStav stav)
    {
        var existing = await db.Vyzvy
            .Where(v => v.ProjektId == projectId && v.Rok == 2026 && v.PoradoveVRoce == poradove)
            .Select(v => (int?)v.Id).FirstOrDefaultAsync();
        if (existing is int id) { return id; }

        var vyzva = new VyzvaEntity
        {
            ProjektId = projectId,
            Kod = $"{poradove}/2026",
            PoradoveVRoce = poradove,
            Rok = 2026,
            Stav = stav,
            DatumZalozeni = new DateTime(2026, 3, 1),
            ZalozilOsobaId = ownerId,
            MistoPlneniSnapshot = "FIS (EIS): VZ 8201",
            CisloRamcoveSmlouvySnapshot = Smlouva,
        };
        db.Vyzvy.Add(vyzva);
        await db.SaveChangesAsync();
        return vyzva.Id;
    }

    private async Task<string> LoadAsync(int projectId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        return await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?rok=2026&asUser={_fixture.AdminOsobaId}");
    }

    [Fact]
    public async Task Lista_JeNadLayoutem_APoradiJeRokUzavritNovaVyzva()
    {
        var s = await SeedAsync();
        var html = await LoadAsync(s.ProjectId);

        var lista = html.IndexOf("data-vyzvy-toolbar", StringComparison.Ordinal);
        var layout = html.IndexOf("vyzvy-layout", StringComparison.Ordinal);
        var rok = html.IndexOf("data-vyzvy-rok", StringComparison.Ordinal);
        var uzavrit = html.IndexOf("data-vyzvy-uzavrit", StringComparison.Ordinal);
        var nova = html.IndexOf("nova-vyzva-modal", StringComparison.Ordinal);

        lista.Should().BeGreaterThan(-1, "lišta vpravo nahoře musí být vyrenderovaná");
        layout.Should().BeGreaterThan(lista, "lišta je nad dvousloupcovým layoutem");
        rok.Should().BeGreaterThan(lista).And.BeLessThan(layout, "výběr roku je v liště");
        uzavrit.Should().BeGreaterThan(rok, "Uzavrit je vpravo od roku");
        nova.Should().BeGreaterThan(uzavrit, "Nova vyzva je uplne vpravo");
    }

    [Fact]
    public async Task Rail_UzNeobsahujeVyberRoku()
    {
        var s = await SeedAsync();
        var html = await LoadAsync(s.ProjectId);

        // Řezat od vyzvy-rail-list nestačí — výběr roku byl NAD ním a do výřezu by nespadl.
        // Bereme celý <nav class="vyzvy-rail"> až po obsah vpravo.
        var start = html.IndexOf("class=\"vyzvy-rail\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "rail musí být vyrenderovaný");
        var end = html.IndexOf("vyzvy-content", start, StringComparison.Ordinal);
        var rail = end < 0 ? html[start..] : html[start..end];

        rail.Should().NotContain("data-vyzvy-rok", "výběr roku se přestěhoval do lišty");
    }

    [Fact]
    public async Task TlacitkoUzavrit_JeVychoziSkryte_BufferNeniVyzva()
    {
        var s = await SeedAsync();
        var html = await LoadAsync(s.ProjectId);

        var uzavrit = html.IndexOf("data-vyzvy-uzavrit", StringComparison.Ordinal);
        uzavrit.Should().BeGreaterThan(-1);

        // Výchozí dlaždice je buffer, ten uzavřít nejde — tlačítko startuje skryté.
        var tag = html[..html.IndexOf('>', uzavrit)];
        tag[tag.LastIndexOf('<')..].Should().Contain("hidden",
            "dokud není vybraná rozpracovaná výzva, tlačítko se nenabízí");
    }

    [Fact]
    public async Task DlazdiceVyzvy_NesePriznakUzavreniPodleStavu()
    {
        var s = await SeedAsync();
        var rail = VyzvyPanelHtml.Rail(await LoadAsync(s.ProjectId));

        var priprava = rail.IndexOf($"data-vyzvy-tile=\"vyzva-{s.PripravaId}\"", StringComparison.Ordinal);
        var odeslano = rail.IndexOf($"data-vyzvy-tile=\"vyzva-{s.OdeslanoId}\"", StringComparison.Ordinal);
        priprava.Should().BeGreaterThan(-1);
        odeslano.Should().BeGreaterThan(-1);

        Dlazdice(rail, priprava).Should().Contain("data-vyzvy-muze-uzavrit=\"true\"",
            "rozpracovanou výzvu lze uzavřít");
        Dlazdice(rail, odeslano).Should().Contain("data-vyzvy-muze-uzavrit=\"false\"",
            "odeslaná výzva je už zamčená");
    }

    [Fact]
    public async Task Dlazdice_NesouPopisekProChipPriTazeni()
    {
        var s = await SeedAsync();
        var rail = VyzvyPanelHtml.Rail(await LoadAsync(s.ProjectId));

        rail.Should().Contain("data-vyzvy-drop-label=\"buffer\"",
            "chip u kurzoru bere text z dlaždice, ne z JS");
        rail.Should().Contain("data-vyzvy-drop-label=\"401/2026\"",
            "u výzvy je v popisku její kód");
    }

    [Fact]
    public async Task DlazdiceVyzvy_JeDvouradkova()
    {
        var s = await SeedAsync();
        var rail = VyzvyPanelHtml.Rail(await LoadAsync(s.ProjectId));

        rail.Should().Contain("vyzvy-tile-head",
            "první řádek nese kód a stav vedle sebe, druhý je počet PNF");
    }

    [Fact]
    public async Task Tisk_JeVPaticcePanelu_NeVHlavicce()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadAsync(s.ProjectId), s.PripravaId);

        var paticka = pane.IndexOf("vyzvy-pane-footer", StringComparison.Ordinal);
        // 2026-09-08: kotva byla data-print-trigger, jenže výběr formátu u výzvy zanikl.
        var tisk = pane.IndexOf("data-vyzva-word", StringComparison.Ordinal);

        paticka.Should().BeGreaterThan(-1, "panel výzvy má patičku");
        tisk.Should().BeGreaterThan(paticka, "tisk je vpravo dole, ne v hlavičce");
    }

    /// <summary>
    /// Výběr formátu u výzvy zrušen 2026-09-08 — pracovník vždycky potřebuje Word.
    /// Náhled zůstává a otevírá tiskovou podobu bez dialogu tisku.
    /// </summary>
    [Fact]
    public async Task Paticka_MaNahledPredTiskem_ABezVyberuFormatu()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadAsync(s.ProjectId), s.PripravaId);

        var nahled = pane.IndexOf("data-vyzva-nahled", StringComparison.Ordinal);
        var word = pane.IndexOf("data-vyzva-word", StringComparison.Ordinal);

        nahled.Should().BeGreaterThan(-1, "náhled musí být vyrenderovaný");
        word.Should().BeGreaterThan(nahled, "Náhled je hned vlevo vedle Tisku");

        pane.Should().NotContain("data-print-trigger",
            "výběr PDF/Word u výzvy zrušen — zbyl jen Word");
    }

    [Fact]
    public async Task Buffer_NemaAniNahledAniTisk()
    {
        var s = await SeedAsync();
        var buffer = VyzvyPanelHtml.BufferPane(await LoadAsync(s.ProjectId));

        buffer.Should().NotContain("data-vyzva-nahled");
        buffer.Should().NotContain("data-vyzva-word", "buffer není dokument");
    }

    [Fact]
    public async Task PnfKarta_MaTitulekAMetaRadek()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadAsync(s.ProjectId), s.PripravaId);

        pane.Should().Contain("vyzvy-pnf-title", "první řádek karty je číslo a název");
        pane.Should().Contain("vyzvy-pnf-meta", "druhý řádek nese cenu");
    }

    private static string Dlazdice(string rail, int start)
    {
        var next = rail.IndexOf("data-vyzvy-tile=\"", start + 1, StringComparison.Ordinal);
        return next < 0 ? rail[start..] : rail[start..next];
    }
}
