using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-14 — kontrakt mezi tím, co editor vykreslí, a tím, co server uloží. Chyběl a
/// proto prošly vady D1–D3: testy posílaly příkaz rovnou službě. Tady se editor vykreslí, pole
/// harmonogramu se z HTML vytáhnou tak, jak by je odeslal prohlížeč (pm-date-field = skryté pole
/// s iso-value), a odešlou se na /Zaznamy/Save.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleEditorContractTests
{
    private static readonly DateTime A = new(2026, 10, 5);
    private readonly ApiSqlFixture _fixture;

    public ScheduleEditorContractTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Pole harmonogramu tak, jak je odešle prohlížeč.</summary>
    private static Dictionary<string, string> PoleHarmonogramu(string html)
    {
        var pole = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(html, @"<(input|pm-date-field)\b[^>]*>", RegexOptions.Singleline))
        {
            var tag = m.Value;
            var name = Regex.Match(tag, "name=\"([^\"]+)\"").Groups[1].Value;
            if (!name.StartsWith("HarmonogramHodnoty[", StringComparison.Ordinal) && name != "HarmonogramRezim")
            {
                continue;
            }
            var hodnota = m.Groups[1].Value == "pm-date-field"
                ? Regex.Match(tag, "iso-value=\"([^\"]*)\"").Groups[1].Value
                : Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value;
            pole.Add(name, hodnota); // Add: duplicitní pole = chyba kontraktu
        }
        return pole;
    }

    private async Task<(int RecordId, int ProjectId, string SubsystemCode, string StatusCode, int OwnerId)> ZalozUkolAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync(marker);
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync(marker + "SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker);
        await using var db = _fixture.CreateDbContext();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = new DateTime(2026, 10, 1).AddDays(poradi * 7),
                SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        var subsystemCode = await db.Subsystemy.Where(x => x.Id == subsystemId).Select(x => x.Kod).SingleAsync();
        var statusCode = await db.CiselnikStavuUkolu.OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        return (recordId, projectId, subsystemCode, statusCode, ownerId);
    }

    private async Task NastavKrokAsync(int recordId, int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
    {
        await using var db = _fixture.CreateDbContext();
        var k = await db.ZaznamHarmonogramKroky.SingleAsync(x => x.ZaznamId == recordId && x.Poradi == poradi);
        k.SkutecnostDatum = datum;
        k.SkutecnostZdroj = (byte)zdroj;
        k.SkutecnostRezim = (byte)rezim;
        await db.SaveChangesAsync();
    }

    private async Task<ZaznamHarmonogramKrokEntity> KrokAsync(int recordId, int poradi)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(x => x.ZaznamId == recordId && x.Poradi == poradi);
    }

    private async Task<string> EditorAsync(HttpClient client, int recordId)
        => await client.GetStringAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");

    private async Task UlozAsync(HttpClient client, (int RecordId, int ProjectId, string SubsystemCode, string StatusCode, int OwnerId) z,
        string html, Action<Dictionary<string, string>> uprav)
    {
        var pole = PoleHarmonogramu(html);
        uprav(pole);
        var recordVersion = Regex.Match(html, "name=\"RecordVersion\" value=\"([^\"]*)\"").Groups[1].Value;
        var form = new List<(string, string)>
        {
            ("Id", z.RecordId.ToString()), ("ProjektId", z.ProjectId.ToString()), ("Kategorie", "U"),
            ("Stav", z.StatusCode), ("Nazev", "Kontrakt harmonogramu"), ("VlastnikId", z.OwnerId.ToString()),
            ("DatumZalozeni", "2026-10-01"), ("TerminUkonceni", "2026-12-31"), ("Subsystem", z.SubsystemCode),
            ("EditorTab", "schedule"), ("RecordVersion", recordVersion)
        };
        form.AddRange(pole.Select(p => (p.Key, p.Value)));
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}", ApiTestHttpHelper.BuildForm(form.ToArray())));
        var result = await ApiTestHttpHelper.ReadModalResultAsync(response);
        result.Ok.Should().BeTrue(result.Message);
    }

    [Fact]
    public async Task Editor_KazdyKrokMaJedinePoleSkutecnosti_BezManualActualKroky()
    {
        var z = await ZalozUkolAsync("APICONTR1");
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var html = await EditorAsync(client, z.RecordId);

        html.Should().NotContain("ManualActualKroky", "skutečnost jde jediným seznamem (R1)");
        html.Should().NotContain("data-manual-zdroj-dropdown", "volba Ručně / Z vyjádření je mrtvý kód (D5)");
        var pole = PoleHarmonogramu(html);
        for (var i = 0; i < 10; i++)
        {
            pole.Should().ContainKey($"HarmonogramHodnoty[{i}].Poradi");
            pole.Should().ContainKey($"HarmonogramHodnoty[{i}].SkutecnostDatum", $"krok na řádku {i} musí mít pole skutečnosti");
        }
        pole["HarmonogramHodnoty[2].SkutecnostDatum"].Should().Be("2026-10-05", "uložené ruční datum je předvyplněné (D2)");
        pole["HarmonogramHodnoty[2].SkutecnostDatumPuvodni"].Should().Be("2026-10-05");
    }

    /// <summary>D1 + D2 — po uloženém ručním datu jde změnit, smazat i vyplnit pozdější kroky.</summary>
    [Fact]
    public async Task Ulozeni_PoUlozenemRucnimDatu_ZmenaSmazaniADalsiKroky()
    {
        var z = await ZalozUkolAsync("APICONTR2");
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        await NastavKrokAsync(z.RecordId, 7, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        await UlozAsync(client, z, await EditorAsync(client, z.RecordId), pole =>
        {
            pole["HarmonogramHodnoty[2].SkutecnostDatum"] = "";            // smazat krok 3
            pole["HarmonogramHodnoty[6].SkutecnostDatum"] = "2026-10-22";  // změnit krok 7
            pole["HarmonogramHodnoty[3].SkutecnostDatum"] = "2026-10-09";  // krok 4 za bývalou dírou
            pole["HarmonogramHodnoty[4].SkutecnostDatum"] = "2026-10-12";  // krok 5 (vždy ruční)
        });

        (await KrokAsync(z.RecordId, 3)).SkutecnostDatum.Should().BeNull();
        (await KrokAsync(z.RecordId, 7)).SkutecnostDatum.Should().Be(new DateTime(2026, 10, 22));
        (await KrokAsync(z.RecordId, 4)).SkutecnostDatum.Should().Be(new DateTime(2026, 10, 9));
        (await KrokAsync(z.RecordId, 5)).SkutecnostDatum.Should().Be(new DateTime(2026, 10, 12));
    }

    /// <summary>D3 — vypnutí automatiky a uložení bez úprav nesmaže data automatu.</summary>
    [Fact]
    public async Task Ulozeni_VypnutiAutomatikyBezUprav_NechaDataAutomatu()
    {
        var z = await ZalozUkolAsync("APICONTR3");
        await NastavKrokAsync(z.RecordId, 1, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);
        await NastavKrokAsync(z.RecordId, 3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        await UlozAsync(client, z, await EditorAsync(client, z.RecordId), pole => pole["HarmonogramRezim"] = "Manual");

        foreach (var poradi in new[] { 1, 3 })
        {
            var k = await KrokAsync(z.RecordId, poradi);
            k.SkutecnostDatum.Should().Be(A);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        }
    }

    /// <summary>
    /// R2 „nic mezi“ — buňky kroků 1/3/4/6/7/10 ukazují režim ZÁZNAMU (stejné pravidlo jako
    /// přepínač), ne režim jednotlivého řádku. Smíšený stav ze starých návrhů tak v editoru
    /// nevypadá jako „automatika zapnutá, ale některá data ruční“.
    /// </summary>
    [Fact]
    public async Task Editor_SmisenyStavRadku_BunkyAutoKrokuMajiRezimZaznamu()
    {
        var z = await ZalozUkolAsync("APICONTR4");
        await NastavKrokAsync(z.RecordId, 1, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var html = await EditorAsync(client, z.RecordId);

        foreach (var poradi in new[] { 1, 3, 4, 6, 7, 10 })
        {
            var rezim = Regex.Match(html,
                $"data-skutecnost-rezim=\"(\\w+)\"[^>]*data-krok-poradi=\"{poradi}\"").Groups[1].Value;
            rezim.Should().Be("Manual", $"záznam je v ručním režimu, krok {poradi} nesmí ukazovat variantu Auto");
        }
    }
}
