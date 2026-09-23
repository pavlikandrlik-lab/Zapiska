using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Rich text pole s textem požadavku se renderuje pod datumy a jen u PNF
/// (spec 2026-09-08 §5.3). Assertace se kotví na atributy — Razor kóduje diakritiku
/// na číselné entity, takže na český text se spolehnout nedá.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExterniOdkazPozadavekRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ExterniOdkazPozadavekRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Seed je idempotentní — Api testy sdílí jednu databázi, takže záznam se nejdřív
    /// dohledá podle markeru v názvu a zakládá se jen když ještě není
    /// (EnsureRecordAsync navzdory jménu zakládá vždy nový).
    /// </summary>
    private async Task<string> NactiEditorAsync(string marker, string typVazby, string cisloTiketu)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"ApiPozadavek{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync($"APIPOZ{marker}");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"APIPOZ{marker}SUB", ownerId);

        int recordId;
        await using (var db = _fixture.CreateDbContext())
        {
            var nazev = $"API pozadavek record {marker}";
            recordId = await db.ProjektoveZaznamy.Where(z => z.Nazev == nazev)
                .Select(z => (int?)z.Id).FirstOrDefaultAsync()
                ?? await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", nazev);

            if (!await db.ZaznamExterniOdkazy.AnyAsync(x => x.ZaznamId == recordId))
            {
                var typeId = await db.CiselnikTypuExternichOdkazu.AsNoTracking()
                    .Where(x => x.Kod == typVazby).Select(x => x.Id).FirstAsync();

                db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
                {
                    ZaznamId = recordId,
                    TypOdkazuId = typeId,
                    Cislo = cisloTiketu
                });
                await db.SaveChangesAsync();
            }
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private Task<string> NactiEditorZaznamuSPnfVazbouAsync() => NactiEditorAsync("PNF", "PNF", "941801");

    private Task<string> NactiEditorZaznamuSNesVazbouAsync() => NactiEditorAsync("NES", "NES", "941802");

    /// <summary>
    /// Typ vazby je u nově zadávaného tiketu ještě neznámý, takže se pole renderuje vždy
    /// a schovává atributem — odhalí ho sync.js po dohledání tiketu. Stejný postup,
    /// jakým se řídí pole ceny a přepínač výzvy.
    /// </summary>
    [Fact]
    public async Task Editor_RenderujePolePozadavku_SHookemARichTextem()
    {
        var html = await NactiEditorZaznamuSPnfVazbouAsync();

        html.Should().Contain("data-external-pozadavek-field",
            "pole musí mít hook, přes který ho sync.js odhalí u PNF");
        html.Should().Contain("data-rich-text=\"true\"",
            "editor jede na existující Quill infrastruktuře, ne na vlastním setupu");
        html.Should().Contain("ExterniVazby[0].Pozadavek",
            "bez správného jména se hodnota neodešle model binderem");
    }

    [Fact]
    public async Task Editor_PolePozadavkuJePodDatumy()
    {
        var html = await NactiEditorZaznamuSPnfVazbouAsync();

        var datumy = html.IndexOf("external-dates", StringComparison.Ordinal);
        var pozadavek = html.IndexOf("data-external-pozadavek-field", StringComparison.Ordinal);

        datumy.Should().BeGreaterThan(-1);
        pozadavek.Should().BeGreaterThan(datumy, "pole patří pod stávající údaje karty");
    }

    /// <summary>
    /// U vazby, která PNF není, je pole schované. Nerenderovat ho vůbec nejde: typ je
    /// u nově zadávaného tiketu ještě neznámý a odhaluje ho až sync.js.
    /// </summary>
    [Fact]
    public async Task Editor_UNesVazby_MaPoleSchovane()
    {
        var html = await NactiEditorZaznamuSNesVazbouAsync();

        var index = html.IndexOf("data-external-pozadavek-field", StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, "pole se renderuje vždy, jen se schovává");

        var tag = html[..html.IndexOf('>', index)];
        tag[tag.LastIndexOf('<')..].Should().Contain("hidden",
            "u jiného typu než PNF se text požadavku nenabízí");
    }
}
