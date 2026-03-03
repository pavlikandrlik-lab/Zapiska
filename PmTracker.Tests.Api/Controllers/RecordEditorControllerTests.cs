using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordEditorControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordEditorControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(null, true, "modal", "modal-overlay")]
    [InlineData(null, false, "page", "record-editor-page-shell")]
    [InlineData("page", true, "page", "record-editor-page-shell")]
    [InlineData("modal", false, "modal", "modal-overlay")]
    public async Task Edit_ShouldRenderExpectedPresentation(
        string? presentation,
        bool ajaxRequest,
        string expectedPresentation,
        string expectedMarker)
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiEditPresenter");
        var projectId = await _fixture.EnsureProjectAsync("APIRED1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "UKOL", "API presentation record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var url = $"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}";
        if (!string.IsNullOrWhiteSpace(presentation))
        {
            url += $"&presentation={presentation}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (ajaxRequest)
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain(expectedMarker);
        html.Should().Contain($"data-record-editor-presentation=\"{expectedPresentation}\"");
    }

    [Fact]
    public async Task Save_ShouldReturnPageRefresh_WhenPagePresentationIsRequested()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiEditSaver");
        var projectId = await _fixture.EnsureProjectAsync("APIRED2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB2", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "UKOL", "API save page record");

        await using var dbContext = _fixture.CreateDbContext();
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => new
            {
                x.Id,
                x.ProjektId,
                x.KategorieId,
                x.StavUkoluId,
                x.CisloZaznamu,
                x.Nazev,
                x.Popis,
                x.VlastnikId,
                x.DatumZalozeni,
                x.DatumUkonceni,
                x.SubsystemId
            })
            .FirstAsync();
        var categoryName = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId)
            .Select(x => x.Nazev)
            .FirstAsync();
        var statusName = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId)
            .Select(x => x.Nazev)
            .FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId)
            .Select(x => x.Kod)
            .FirstAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var returnUrl = $"/Projekty/Detail/{projectId}?tab=zaznamy";
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", record.Id.ToString()),
                ("ProjektId", record.ProjektId.ToString()),
                ("Kategorie", categoryName),
                ("Stav", statusName),
                ("Nazev", $"{record.Nazev} updated"),
                ("Popis", record.Popis ?? string.Empty),
                ("VlastnikId", record.VlastnikId.ToString()),
                ("DatumZalozeni", record.DatumZalozeni.ToString("yyyy-MM-dd")),
                ("TerminUkonceni", record.DatumUkonceni.ToString("yyyy-MM-dd")),
                ("Subsystem", subsystemCode),
                ("CisloZaznamu", record.CisloZaznamu.ToString()),
                ("EditorTab", "basic"),
                ("Presentation", "page"),
                ("ReturnUrl", returnUrl)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("page");
        payload.RefreshUrl.Should().Contain(returnUrl);
        payload.RefreshUrl.Should().Contain("restoreRecordEditorState=1");
    }

    [Fact]
    public async Task Save_ShouldPersistNegativeScheduleActual_WhenScheduleTabIsSubmitted()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiScheduleSaver");
        var projectId = await _fixture.EnsureProjectAsync("APIRED3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB3", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "UKOL", "API signed schedule record");

        await using var dbContext = _fixture.CreateDbContext();
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => new
            {
                x.Id,
                x.ProjektId,
                x.KategorieId,
                x.StavUkoluId,
                x.CisloZaznamu,
                x.Nazev,
                x.Popis,
                x.VlastnikId,
                x.DatumZalozeni,
                x.DatumUkonceni,
                x.SubsystemId,
                x.HarmonogramSablonaVerze
            })
            .FirstAsync();
        var categoryName = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId)
            .Select(x => x.Nazev)
            .FirstAsync();
        var statusName = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId)
            .Select(x => x.Nazev)
            .FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId)
            .Select(x => x.Kod)
            .FirstAsync();
        var firstStepOrder = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .Select(x => x.KrokPoradi)
            .FirstAsync();
        var durationTypeId = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && !x.JeZpozdeni && x.KrokPoradi == firstStepOrder)
            .Select(x => x.Id)
            .FirstAsync();
        var delayTypeId = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && x.JeZpozdeni && x.KrokPoradi == firstStepOrder)
            .Select(x => x.Id)
            .FirstAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", record.Id.ToString()),
                ("ProjektId", record.ProjektId.ToString()),
                ("Kategorie", categoryName),
                ("Stav", statusName),
                ("Nazev", record.Nazev),
                ("Popis", record.Popis ?? string.Empty),
                ("VlastnikId", record.VlastnikId.ToString()),
                ("DatumZalozeni", record.DatumZalozeni.ToString("yyyy-MM-dd")),
                ("TerminUkonceni", record.DatumUkonceni.ToString("yyyy-MM-dd")),
                ("Subsystem", subsystemCode),
                ("CisloZaznamu", record.CisloZaznamu.ToString()),
                ("EditorTab", "schedule"),
                ("Presentation", "modal"),
                ("HarmonogramHodnoty[0].TypId", durationTypeId.ToString()),
                ("HarmonogramHodnoty[0].Hodnota", "5"),
                ("HarmonogramHodnoty[1].TypId", delayTypeId.ToString()),
                ("HarmonogramHodnoty[1].Hodnota", "-2")));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();

        await using var verificationDbContext = _fixture.CreateDbContext();
        var savedDelay = await verificationDbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypId == delayTypeId)
            .Select(x => (int?)x.HodnotaInt)
            .SingleOrDefaultAsync();
        var savedDuration = await verificationDbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.TypId == durationTypeId)
            .Select(x => (int?)x.HodnotaInt)
            .SingleOrDefaultAsync();

        savedDuration.Should().Be(5);
        savedDelay.Should().Be(-2);
    }
}
