using System.Net;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Tisk výzvy (spec 2026-09-07-vyzvy-dokonceni-design §9). Word i PDF jedou ze stejné
/// projekce, tlačítko přes sdílený chooser formátu.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyTiskTests
{
    private const string WordContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly ApiSqlFixture _fixture;

    public VyzvyTiskTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Idempotentní seed — fixture sdílí databázi mezi testy a unique index
    /// ux_vyzvy_smlouva_rok_poradove je globální.
    /// </summary>
    private async Task<(int ProjectId, int VyzvaId)> SeedAsync()
    {
        const string zaznamMarker = "Zaznam k tisku";

        var ownerId = await _fixture.EnsurePersonAsync("ApiVyzvyTiskOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIVYZTISK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIVYZTISK_SUB", ownerId);

        int recordId;
        await using (var lookup = _fixture.CreateDbContext())
        {
            recordId = await lookup.ProjektoveZaznamy.AsNoTracking()
                .Where(z => z.ProjektId == projectId && z.Nazev == zaznamMarker)
                .Select(z => z.Id).FirstOrDefaultAsync();
        }
        if (recordId == 0)
        {
            recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", zaznamMarker);
        }

        await using var dbContext = _fixture.CreateDbContext();
        var projekt = await dbContext.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = "APIVYZ-SML-TISK";
        await dbContext.SaveChangesAsync();

        var vyzva = await dbContext.Vyzvy
            .FirstOrDefaultAsync(v => v.ProjektId == projectId && v.Rok == 2026 && v.PoradoveVRoce == 701);
        if (vyzva is null)
        {
            vyzva = new VyzvaEntity
            {
                ProjektId = projectId, Kod = "701/2026", PoradoveVRoce = 701, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 2, 1), ZalozilOsobaId = ownerId,
                MistoPlneniSnapshot = "FIS (EIS): VZ 8201", CisloRamcoveSmlouvySnapshot = "APIVYZ-SML-TISK",
            };
            dbContext.Vyzvy.Add(vyzva);
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.ZaznamExterniOdkazy.AnyAsync(ev => ev.Cislo == "884477"))
        {
            var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
                .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
            dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId, TypOdkazuId = pnfTypeId, Cislo = "884477",
                ZaradidDoVyzvy = true, VyzvaId = vyzva.Id,
            });
            await dbContext.SaveChangesAsync();
        }

        return (projectId, vyzva.Id);
    }

    private string RenderedPrintHtml()
        => _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>().LastHtml
           ?? throw new InvalidOperationException("Generátor PDF nedostal žádné HTML.");

    private async Task<string> LoadPanelAsync(int projectId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        return await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?rok=2026&asUser={_fixture.AdminOsobaId}");
    }

    // ---- 7A: tlačítko ----

    /// <summary>
    /// 2026-09-08: sdílený chooser formátu u výzvy zanikl — do spisové služby jde vždy
    /// Word. Zbyly dva odkazy: Náhled na tiskovou podobu a Tisk výzvy na Word. Obojí
    /// musí dál nést projektId, jinak by project-scoped policy udělala tichý globální
    /// check (memory feedback_permission_policy_needs_projektid_in_route).
    /// </summary>
    [Fact]
    public async Task PanelVyzvy_NabidneNahledAWord_BezChooseru()
    {
        var (projectId, vyzvaId) = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadPanelAsync(projectId), vyzvaId);

        pane.Should().NotContain("data-print-trigger", "výběr formátu u výzvy zrušen");
        pane.Should().Contain($"/Export/Vyzva/{vyzvaId}/Tisk", "náhled míří na tiskovou podobu");
        pane.Should().Contain($"/Export/Vyzva/{vyzvaId}/Word", "tisk stahuje Word");
        pane.Should().Contain($"projektId={projectId}",
            "bez projektId by project-scoped policy udělala tichý globální check");
    }

    [Fact]
    public async Task BufferNemaTisk()
    {
        var (projectId, _) = await SeedAsync();
        var buffer = VyzvyPanelHtml.BufferPane(await LoadPanelAsync(projectId));

        buffer.Should().NotContain("data-print-trigger", "buffer není dokument, nemá co tisknout");
        buffer.Should().NotContain("data-vyzva-nahled", "ani náhled");
        buffer.Should().NotContain("data-vyzva-word", "ani Word");
    }

    // ---- 7C: PDF ----

    [Fact]
    public async Task VyzvaTisk_VrátiDokumentSObsahemVyzvy()
    {
        var (projectId, vyzvaId) = await SeedAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        // Tisk vrací PDF, ne HTML. Kontrolovaná šablona se proto bere ze vstupu
        // generátoru — stejný vzor jako ExportControllerTests.
        var html = RenderedPrintHtml();
        html.Should().Contain("701/2026", "dokument nese kód výzvy");
        html.Should().Contain("884477", "a číslo PNF v tabulce požadavků");
        html.Should().Contain("APIVYZ-SML-TISK", "a číslo rámcové dohody");
    }

    [Fact]
    public async Task VyzvaTisk_CiziProjektId_JeNotFound()
    {
        var (_, vyzvaId) = await SeedAsync();
        var jinyProjekt = await _fixture.EnsureProjectAsync("APIVYZTISKJINY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Tisk?projektId={jinyProjekt}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "jinak by šlo autorizovat proti spravovanému projektu a tisknout cizí výzvu");
    }

    [Fact]
    public async Task VyzvaTisk_NeexistujiciVyzva_JeNotFound()
    {
        var (projectId, _) = await SeedAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/999999/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- 7D: Word ----

    [Fact]
    public async Task VyzvaWord_VratiPlatnyDocx()
    {
        var (projectId, vyzvaId) = await SeedAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Word?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(WordContentType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty();

        using var stream = new MemoryStream(bytes);
        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
        var text = doc.MainDocumentPart!.Document.InnerText;

        text.Should().Contain("701/2026", "dokument nese kód výzvy");
        text.Should().Contain("884477", "a číslo PNF");
    }

    [Fact]
    public async Task VyzvaWord_CiziProjektId_JeNotFound()
    {
        var (_, vyzvaId) = await SeedAsync();
        var jinyProjekt = await _fixture.EnsureProjectAsync("APIVYZTISKJINY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Word?projektId={jinyProjekt}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Text požadavku se do náhledu dostane jako formátovaný rich text a skript se do něj
    /// nedostane (spec 2026-09-08 §5.7). Obsah PDF se ověřuje přes HTML, které dostal
    /// generátor — samotná odpověď je falešný PDF payload. Kotví se na ASCII, protože
    /// Razor kóduje diakritiku na číselné entity.
    /// </summary>
    [Fact]
    public async Task VyzvaTisk_NahledVysaziTextPozadavku_BezSkriptu()
    {
        var (projectId, vyzvaId) = await SeedAsync();

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var vazba = await dbContext.ZaznamExterniOdkazy.FirstAsync(ev => ev.Cislo == "884477");
            vazba.Pozadavek = "<p>Chceme <strong>sestavu</strong>.</p><script>alert(1)</script>";
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = RenderedPrintHtml();

        html.Should().Contain("vyzva-pozadavek", "text se sází do vlastního bloku s TNR 12");
        html.Should().Contain("<strong>sestavu</strong>",
            "formátování z editoru se v náhledu zachová, nesmí se zakódovat");
        html.Should().NotContain("<script", "sanitizace běží i při stavbě dokumentu");
    }

    /// <summary>
    /// Náhled má strukturu finálního vzoru, stejnou jako Word (spec 2026-09-10 část B).
    /// Kotví se na data-* atributy — text je v HTML zakódovaný. ServiceDesk je v testech
    /// vypnutý, takže PNF nemá akceptovanou kalkulaci a v sekci 2 nesmí být tabulka.
    /// </summary>
    [Fact]
    public async Task VyzvaTisk_NahledMaStrukturuVzoru()
    {
        var (projectId, vyzvaId) = await SeedAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = RenderedPrintHtml();

        foreach (var sekce in Enumerable.Range(1, 7))
        {
            html.Should().Contain($"data-vyzva-sekce=\"{sekce}\"");
        }
        html.Should().Contain("data-vyzva-poradi=\"I.\"", "pořadí požadavků římsky");
        html.Should().Contain("data-vyzva-strucne-popisy", "stručné popisy patří do sekce 1");
        html.Should().Contain("data-vyzva-tabulka=\"rekapitulace-cinnosti\"");
        html.Should().Contain("data-vyzva-tabulka=\"rekapitulace-licence\"");
        html.Should().NotContain("data-vyzva-tabulka=\"cinnosti\"", "bez akceptované kalkulace žádná tabulka");
        html.Should().NotContain("XXXX", "prázdná pole zůstávají prázdná, ne zástupná");
        html.Should().NotContain("Vazba na PMP");
    }
}
