using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
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
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API presentation record");

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
        html.Should().Contain("data-record-owner-picker");
        html.Should().Contain("office-searchbox\" data-floating-anchor");
        html.Should().Contain("name=\"Cil\"");
    }

    [Fact]
    public async Task RecordCardPartial_ShouldRenderGoalInSubtitle_AndDescriptionInExpandedBody()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCardGoalOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCIL");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCILSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API card goal record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
            record.Cil = "Jednoradkovy cil pro kartu";
            record.Popis = "Detailni popis po rozbaleni";
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("record-goal-subtitle");
        html.Should().Contain("Jednoradkovy cil pro kartu");
        html.Should().Contain("<span class=\"label\">Popis:</span>");
        html.Should().Contain("Detailni popis po rozbaleni");
    }

    [Fact]
    public async Task Save_ShouldReturnPageRefresh_WhenPagePresentationIsRequested()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiEditSaver");
        var projectId = await _fixture.EnsureProjectAsync("APIRED2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB2", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API save page record");

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
                x.Cil,
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
                ("Cil", record.Cil ?? string.Empty),
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
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
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
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API signed schedule record");

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
                x.Cil,
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
                ("Cil", record.Cil ?? string.Empty),
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

    [Fact]
    public async Task Edit_ShouldRenderScheduleMiniGantt_WithAlignedAxis_WithoutPerStepDuplicateBars()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiEditScheduleAxis");
        var projectId = await _fixture.EnsureProjectAsync("APIRED4");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB4", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API schedule layout record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}&presentation=page");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-mini-gantt-grid");
        html.Should().Contain("schedule-mini-gantt-axis-track");
        html.Should().NotContain("data-schedule-step-planned");
        html.Should().NotContain("data-schedule-step-actual");
        html.Should().NotContain("schedule-step-gantt-stack");
        Regex.Matches(html, "data-schedule-axis").Count.Should().Be(1);
    }

    [Fact]
    public async Task Save_ShouldPersistEstimatedExternalLinkPrice_OnlyForPmpAndPnf()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiExternalPriceSaver");
        var projectId = await _fixture.EnsureProjectAsync("APIRED5");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB5", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API external price record");

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
                x.Cil,
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
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", record.Id.ToString()),
                ("ProjektId", record.ProjektId.ToString()),
                ("Kategorie", categoryName),
                ("Stav", statusName),
                ("Nazev", record.Nazev),
                ("Cil", record.Cil ?? string.Empty),
                ("Popis", record.Popis ?? string.Empty),
                ("VlastnikId", record.VlastnikId.ToString()),
                ("DatumZalozeni", record.DatumZalozeni.ToString("yyyy-MM-dd")),
                ("TerminUkonceni", record.DatumUkonceni.ToString("yyyy-MM-dd")),
                ("Subsystem", subsystemCode),
                ("CisloZaznamu", record.CisloZaznamu.ToString()),
                ("EditorTab", "external"),
                ("Presentation", "modal"),
                ("ExterniVazby[0].Typ", "PMP"),
                ("ExterniVazby[0].Cislo", "PMP-123"),
                ("ExterniVazby[0].PredpokladanaCena", "125000.50"),
                ("ExterniVazby[1].Typ", "NES"),
                ("ExterniVazby[1].Cislo", "NES-456"),
                ("ExterniVazby[1].PredpokladanaCena", "999.99")));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();

        await using var verificationDbContext = _fixture.CreateDbContext();
        var savedLinks = await (
                from link in verificationDbContext.ZaznamExterniOdkazy.AsNoTracking()
                join type in verificationDbContext.CiselnikTypuExternichOdkazu.AsNoTracking() on link.TypOdkazuId equals type.Id
                where link.ZaznamId == recordId
                select new { type.Kod, link.Cislo, link.PredpokladanaCena })
            .ToListAsync();

        savedLinks.Should().ContainSingle(x => x.Kod == "PMP" && x.Cislo == "PMP-123" && x.PredpokladanaCena == 125000.50m);
        savedLinks.Should().ContainSingle(x => x.Kod == "NES" && x.Cislo == "NES-456" && x.PredpokladanaCena == null);
    }

    [Fact]
    public async Task Create_ShouldRenderScheduleActualInput_WithoutClientSideMinimumClamp()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCreateSignedDelay");
        var projectId = await _fixture.EnsureProjectAsync("APIRED4");
        await _fixture.EnsureSubsystemAsync("APIREDSUB4", ownerId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Create?projektId={projectId}&asUser={_fixture.AdminOsobaId}&presentation=page");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var delayInputMatch = Regex.Match(
            html,
            "<input[^>]*data-schedule-delay[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        delayInputMatch.Success.Should().BeTrue(html);
        delayInputMatch.Value.Should().NotContain("min=", "skutečnost musí podporovat záporné hodnoty už před prvním uložením");
    }

    [Fact]
    public async Task DeleteRecordModal_ShouldRenderDependencySummary_ForEditableRecord()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiDeleteModalSummaryOwner");
        var collaboratorId = await _fixture.EnsurePersonAsync("ApiDeleteModalSummaryCollaborator");
        var projectId = await _fixture.EnsureProjectAsync("APIRED7");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB7", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API delete summary record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9801);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var externalTypeId = await dbContext.CiselnikTypuExternichOdkazu
                .Where(x => x.Kod == "PMP")
                .Select(x => x.Id)
                .FirstAsync();
            var scheduleTypeId = await dbContext.CiselnikHarmonogramTypu
                .Where(x => !x.JeZpozdeni)
                .OrderBy(x => x.KrokPoradi)
                .Select(x => x.Id)
                .FirstAsync();
            var statusId = await dbContext.ProjektoveZaznamy
                .Where(x => x.Id == recordId)
                .Select(x => x.StavUkoluId)
                .FirstAsync()
                ?? throw new InvalidOperationException("Test record is missing task state.");

            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Delete summary comment",
                DatumVyjadreni = DateTime.UtcNow
            });
            dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = externalTypeId,
                Cislo = "PMP-DELETE-SUMMARY"
            });
            dbContext.ZaznamSpoluprace.Add(new ZaznamSpolupraceEntity
            {
                ZaznamId = recordId,
                OsobaId = collaboratorId
            });
            dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = recordId,
                TypId = scheduleTypeId,
                HodnotaInt = 5,
                UpdatedAt = DateTime.UtcNow
            });
            dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
            {
                ZaznamId = recordId,
                PuvodniStav = statusId,
                NovyStav = statusId,
                DatumZmeny = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/DeleteRecordModal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("record-delete-modal-title");
        html.Should().NotContain("data-confirm-submit-checkbox");
        html.Should().Contain("Vyjádření:</span> 1");
        html.Should().Contain("Externí vazby:</span> 1");
        html.Should().Contain("Spolupráce:</span> 1");
        html.Should().Contain("Harmonogram:</span> 1");
    }

    [Fact]
    public async Task DeleteRecord_ShouldReturnAjaxSuccess_AndDeleteRelatedData()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiDeleteOwner");
        var collaboratorId = await _fixture.EnsurePersonAsync("ApiDeleteCollaborator");
        var projectId = await _fixture.EnsureProjectAsync("APIRED8");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSUB8", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API hard delete record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9802);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var externalTypeId = await dbContext.CiselnikTypuExternichOdkazu
                .Where(x => x.Kod == "PMP")
                .Select(x => x.Id)
                .FirstAsync();
            var scheduleTypeId = await dbContext.CiselnikHarmonogramTypu
                .Where(x => !x.JeZpozdeni)
                .OrderBy(x => x.KrokPoradi)
                .Select(x => x.Id)
                .FirstAsync();
            var typeId = await dbContext.CiselnikTypuUkolu
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync();
            var statusId = await dbContext.ProjektoveZaznamy
                .Where(x => x.Id == recordId)
                .Select(x => x.StavUkoluId)
                .FirstAsync()
                ?? throw new InvalidOperationException("Test record is missing task state.");

            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Delete me",
                DatumVyjadreni = DateTime.UtcNow
            });
            dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = externalTypeId,
                Cislo = "PMP-DELETE"
            });
            dbContext.ZaznamSpoluprace.Add(new ZaznamSpolupraceEntity
            {
                ZaznamId = recordId,
                OsobaId = collaboratorId
            });
            dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = recordId,
                TypId = scheduleTypeId,
                HodnotaInt = 3,
                UpdatedAt = DateTime.UtcNow
            });
            dbContext.ZaznamHistorieZmenTypu.Add(new ZaznamHistorieZmenTypuEntity
            {
                ZaznamId = recordId,
                PuvodniTypId = typeId,
                NovyTypId = typeId,
                DatumZmeny = DateTime.UtcNow,
                ZmenilOsobaId = ownerId
            });
            dbContext.ZaznamHistorieTerminu.Add(new ZaznamHistorieTerminuEntity
            {
                ZaznamId = recordId,
                PuvodniDatum = DateTime.Today,
                NoveDatum = DateTime.Today.AddDays(1),
                DatumZmeny = DateTime.UtcNow,
                Duvod = "API delete test"
            });
            dbContext.ZaznamHistorieVlastnik.Add(new ZaznamHistorieVlastnikEntity
            {
                ZaznamId = recordId,
                PuvodniVlastnik = ownerId,
                NovyVlastnik = ownerId,
                DatumZmeny = DateTime.UtcNow
            });
            dbContext.ZaznamHistorieSubsystem.Add(new ZaznamHistorieSubsystemEntity
            {
                ZaznamId = recordId,
                PuvodniSubsystem = subsystemId,
                NovySubsystem = subsystemId,
                DatumZmeny = DateTime.UtcNow
            });
            dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
            {
                ZaznamId = recordId,
                PuvodniStav = statusId,
                NovyStav = statusId,
                DatumZmeny = DateTime.UtcNow
            });
            dbContext.ZaznamHistorieStavuProjektu.Add(new ZaznamHistorieStavuProjektuEntity
            {
                ZaznamId = recordId,
                PuvodniStav = statusId,
                NovyStav = statusId,
                DatumZmeny = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/DeleteRecord?asUser={_fixture.AdminOsobaId}&returnUrl=%2FProjekty%2FDetail%2F{projectId}%3Ftab%3Dzaznamy&uiContext=project&tab=zaznamy",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("projekty-detail-zaznamy-preserve");

        await using var verificationDbContext = _fixture.CreateDbContext();
        (await verificationDbContext.ProjektoveZaznamy.AnyAsync(x => x.Id == recordId)).Should().BeFalse();
        (await verificationDbContext.Vyjadreni.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await verificationDbContext.ZaznamExterniOdkazy.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await verificationDbContext.ZaznamSpoluprace.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await verificationDbContext.ZaznamHarmonogramHodnoty.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();
        (await verificationDbContext.ZaznamHistorieZmenTypu.AnyAsync(x => x.ZaznamId == recordId)).Should().BeFalse();

        var auditExists = await verificationDbContext.AuthzAuditLog.AnyAsync(x =>
            x.EntityType == "projektove_zaznamy" &&
            x.EntityId == recordId.ToString() &&
            x.Action == "hard_delete");
        auditExists.Should().BeTrue();
    }
}
