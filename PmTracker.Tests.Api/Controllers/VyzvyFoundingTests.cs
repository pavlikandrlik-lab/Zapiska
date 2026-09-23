using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Zakládání výzvy přes reálný endpoint. Od 2026-09-07 se číslo výzvy domlouvá se SVA
/// mimo aplikaci, zadává se ručně a výzva vzniká prázdná
/// (spec 2026-09-07-vyzvy-dokonceni-design §5).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyFoundingTests
{
    private readonly ApiSqlFixture _fixture;

    public VyzvyFoundingTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Projekt bez místa plnění a čísla rámcové smlouvy nejde použít — z obou se dělají
    /// snapshoty na výzvě. Číslo smlouvy je per test unikátní, protože unique index
    /// ux_vyzvy_smlouva_rok_poradove je globální, ne per projekt.
    /// </summary>
    private async Task<int> EnsureProcurementProjectAsync(string marker, string cisloSmlouvy)
    {
        var projectId = await _fixture.EnsureProjectAsync(marker);

        await using var dbContext = _fixture.CreateDbContext();
        var projekt = await dbContext.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = cisloSmlouvy;
        await dbContext.SaveChangesAsync();

        return projectId;
    }

    [Fact]
    public async Task NovaVyzvaModal_NabidneRucniZadaniCisla()
    {
        var projectId = await EnsureProcurementProjectAsync("APIVYZNOVA1", "APIVYZ-SML-1");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/vyzvy/nova-vyzva-modal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("name=\"PoradoveVRoce\"", "číslo výzvy zadává uživatel ručně");
        html.Should().Contain("APIVYZ-SML-1", "formulář ukazuje rámcovou smlouvu jako kontext");
        html.Should().Contain("data-ajax-submit", "modal jede standardní ajax-submit cestou");
    }

    [Fact]
    public async Task Zalozit_SRucnimCislem_VytvoriPrazdnouVyzvu()
    {
        var projectId = await EnsureProcurementProjectAsync("APIVYZNOVA2", "APIVYZ-SML-2");
        var rok = DateTime.UtcNow.Year;

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/vyzvy/zalozit?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("PoradoveVRoce", "137")));
        var response = await client.SendAsync(request);

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue(payload.Message);
        payload.RefreshScope.Should().Be("vyzvy-panel", "panel se překreslí na místě, obrazovka neuskočí");
        payload.RefreshUrl.Should().Contain($"rok={rok}",
            "uživatel mohl prohlížet starší rok — panel se musí přepnout na rok nové výzvy");

        await using var dbContext = _fixture.CreateDbContext();
        var vyzva = await dbContext.Vyzvy.AsNoTracking()
            .SingleAsync(v => v.ProjektId == projectId && v.PoradoveVRoce == 137);
        vyzva.Kod.Should().Be($"137/{rok}");
        vyzva.CisloRamcoveSmlouvySnapshot.Should().Be("APIVYZ-SML-2");

        payload.UiContext.Should().Be($"vyzva-{vyzva.Id}", "panel skočí rovnou na novou výzvu");

        var polozky = await dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .CountAsync(ev => ev.VyzvaId == vyzva.Id);
        polozky.Should().Be(0, "výzva vzniká prázdná, PNF se do ní přesouvají ručně");
    }

    [Fact]
    public async Task Zalozit_DuplicitniCislo_VratiChybuUPole()
    {
        var projectId = await EnsureProcurementProjectAsync("APIVYZNOVA3", "APIVYZ-SML-3");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        async Task<HttpResponseMessage> ZalozitAsync() => await client.SendAsync(
            ApiTestHttpHelper.BuildAjaxPost(
                $"/vyzvy/zalozit?asUser={_fixture.AdminOsobaId}",
                ApiTestHttpHelper.BuildForm(
                    ("ProjektId", projectId.ToString()),
                    ("PoradoveVRoce", "241"))));

        (await ApiTestHttpHelper.ReadModalResultAsync(await ZalozitAsync())).Ok.Should().BeTrue();

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(await ZalozitAsync());
        payload.Ok.Should().BeFalse("číslo je v rámci smlouvy a roku už obsazené");
        payload.FieldErrors.Should().ContainKey("PoradoveVRoce",
            "chyba patří k poli, ne jen do souhrnu — uživatel opravuje číslo");
    }

    [Fact]
    public async Task Zalozit_CisloMimoRozsah_VratiChybuUPole()
    {
        var projectId = await EnsureProcurementProjectAsync("APIVYZNOVA4", "APIVYZ-SML-4");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(
            ApiTestHttpHelper.BuildAjaxPost(
                $"/vyzvy/zalozit?asUser={_fixture.AdminOsobaId}",
                ApiTestHttpHelper.BuildForm(
                    ("ProjektId", projectId.ToString()),
                    ("PoradoveVRoce", "1000"))));

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse("povolený rozsah je 1–999");
        payload.FieldErrors.Should().ContainKey("PoradoveVRoce");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Vyzvy.AsNoTracking().AnyAsync(v => v.ProjektId == projectId))
            .Should().BeFalse("neplatné číslo nesmí nic založit");
    }

    /// <summary>
    /// Rail nabízí tlačítko jen tehdy, když z projektu jde udělat snapshoty. Bez nich
    /// ukáže důvod — dřív ten důvod builder počítal, ale žádná šablona ho nevypisovala.
    /// </summary>
    [Fact]
    public async Task VyzvyRail_PripravenyProjekt_NabidneModalNoveVyzvy()
    {
        var projectId = await EnsureProcurementProjectAsync("APIVYZNOVA6", "APIVYZ-SML-6");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("nova-vyzva-modal", "tlačítko otevírá modal Nová výzva");
        html.Should().Contain("data-modal-url", "jede standardní modal cestou aplikace");
    }

    [Fact]
    public async Task VyzvyRail_ProjektBezRamcoveSmlouvy_UkazeDuvodMistoTlacitka()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZNOVA7");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?asUser={_fixture.AdminOsobaId}");

        html.Should().NotContain("nova-vyzva-modal", "bez místa plnění nelze výzvu založit");
        // Razor kóduje diakritiku na číselné entity (M&#xED;sto), takže se kotvíme
        // na atribut; obsah hlášky hlídá VyzvyPanelBuilderTests.
        html.Should().Contain("data-vyzvy-zalozit-blokace", "uživatel se musí dozvědět, co doplnit");
    }

    [Fact]
    public async Task NovaVyzvaModal_UkazujeObsazenaCisla()
    {
        var projectId = await EnsureProcurementProjectAsync("APIVYZNOVA5", "APIVYZ-SML-5");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/vyzvy/zalozit?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("PoradoveVRoce", "318"))));

        var html = await client.GetStringAsync(
            $"/vyzvy/nova-vyzva-modal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("318", "modal radí, která čísla už jsou v roce obsazená");
    }
}
