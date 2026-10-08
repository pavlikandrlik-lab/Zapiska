using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectHarmonogramRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectHarmonogramRenderTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Detail_ShouldRenderProjectIdentityInBreadcrumbBar()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectHeaderOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMHDR");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBHDR", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API project header record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Projektová hlavička se přesunula do drobečkové lišty (frame bar pod menu).
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("app-breadcrumb-suffix");   // " | APIHARMHDR"
        html.Should().Contain("APIHARMHDR");
        html.Should().NotContain("project-title-inline");
    }

    [Fact]
    public async Task Detail_ShouldRenderRecordCardsAndTabFallbackLinks_ServerSide()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectRecordsOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMREC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBREC", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API records fallback record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("API records fallback record");
        html.Should().Contain("data-record-grouped-list");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=zaznamy&amp;asUser=");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=harmonogram&amp;asUser=");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=jednani&amp;asUser=");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=tym&amp;asUser=");
    }

    [Fact]
    public async Task Detail_ShouldDefaultOnlyActiveTasksFilter_ToCheckedState()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectActiveFilterOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMACT");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBACT", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API active filter record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Filtr „pouze aktivní úkoly" je gov-form-switch s default checked (bare atribut).
        html.Should().Contain("data-filter-key=\"aktivni\"");
        html.Should().MatchRegex("data-filter-key=\"aktivni\"\\s+checked");
    }

    [Fact]
    public async Task Detail_ShouldRenderTeamTabSearch_AndSortableTables()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectTeamTabOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMTYM");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBTYM", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API team tab record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=tym&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-tab-panel=\"tym\"");
        html.Should().Contain("data-table-tools-root");
        html.Should().Contain("data-table-tools-search-input");
        html.Should().Contain("data-table-tools-table");
        html.Should().Contain("data-table-sort-button");
        html.Should().Contain("Žádná projektová role neodpovídá zadanému filtru.");
    }

    [Fact]
    public async Task Detail_ShouldRenderCompactOverview_WithSeparateRows_AndAxisBelow()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARM1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUB1", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram layered record");

        // Datum-model: plán pro všech 10 kroků, skutečnost na kroku 1 (= vyplněný segment).
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-overview-timeline");
        html.Should().Contain(">Plán<");
        html.Should().Contain(">Skutečnost<");
        html.Should().Contain("schedule-overview-track");
        html.Should().Contain("schedule-overview-axis");
        html.Should().Contain("data-timeline-axis");
        html.Should().Contain("data-axis-start=");
        html.Should().Contain("data-axis-end=");
        // „Dnes" a „Termín" jsou od 6128ff5 (2026-07-01) popisky DNES/TERMÍN na ose, ne svislé
        // čáry v pruzích; server dodá jejich pozici v % (JS je vykreslí na ose).
        html.Should().NotContain("schedule-overview-marker", "svislé čáry „Dnes“/„Termín“ v pruzích byly odstraněny");
        var osa = Regex.Match(html, "<div[^>]*data-schedule-axis=\"overview\"[^>]*>").Value;
        osa.Should().MatchRegex("data-axis-today-pct=\"\\d+(\\.\\d+)?\"", "popisek DNES na ose");
        osa.Should().MatchRegex("data-axis-deadline-pct=\"\\d+(\\.\\d+)?\"", "popisek TERMÍN na ose");
        html.Should().NotContain("Legenda: Plán / Skutečnost / Termín úkolu");
        html.Should().NotContain("schedule-layered-track schedule-layered-track--overview");

        var overviewRowsBeforeAxis = Regex.IsMatch(
            html,
            "schedule-overview-row[\\s\\S]*schedule-overview-row[\\s\\S]*schedule-overview-axis",
            RegexOptions.CultureInvariant);

        overviewRowsBeforeAxis.Should().BeTrue("compact overview má mít dvě řádky Plán/Skutečnost a osu až pod nimi");
    }

    [Fact]
    public async Task Detail_ShouldRenderBreakdown_WithOwnAxis_AndTodayLabelOnAxis()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramBreakdownOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARM2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUB2", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram breakdown record");

        // Datum-model: plán pro všech 10 kroků, skutečnost na krocích 1–2.
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-layered-axis");
        html.Should().Contain("data-timeline-axis");
        html.Should().Contain("data-axis-start=");
        html.Should().Contain("data-axis-end=");
        html.Should().Contain("schedule-layered-track schedule-layered-track--step");
        // „Dnes" je od 6128ff5 (2026-07-01) popisek DNES na ose rozpadu, ne čára v pruhu kroku.
        html.Should().NotContain("schedule-layered-marker", "svislá čára „Dnes“ v pruzích kroků byla odstraněna");
        var osa = Regex.Match(html, "<div[^>]*data-schedule-axis=\"breakdown\"[^>]*>").Value;
        osa.Should().MatchRegex("data-axis-today-pct=\"\\d+(\\.\\d+)?\"", "popisek DNES na ose rozpadu");
        osa.Should().MatchRegex("data-axis-deadline-pct=\"\\d+(\\.\\d+)?\"", "popisek TERMÍN na ose rozpadu");
        html.Should().Contain("schedule-layered-legend-swatch today", "legenda rozpadu vysvětluje DNES");
    }

    [Fact]
    public async Task Detail_ShouldSkipZeroDurationSteps_InCompactOverview()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramZeroDurationOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMZERO");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBZERO", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram zero duration record");

        // Datum-model: skutečnost vyplněna na krocích 1 a 3, krok 2 BEZ skutečnosti →
        // ve skutečnost-tracku se absorbuje (nevykreslí vlastní segment).
        const int firstStepOrder = 1;
        const int secondStepOrder = 2;
        const int thirdStepOrder = 3;
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-40), new HashSet<int> { firstStepOrder, thirdStepOrder });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Datum-model: server pozicuje segmenty inline (segment zůstává v DOM, skrytí přes
        // display:none). Skutečnost kroku 2 (bez data) se absorbuje → actual segment kroku 2
        // má display:none; kroky 1 a 3 mají viditelný actual segment (s left/width).
        string ActualSegment(int step) =>
            System.Text.RegularExpressions.Regex.Match(
                html,
                $"data-schedule-segment-kind=\"actual\"\\s+data-step-index=\"{step}\"[\\s\\S]{{0,200}}?style=\"([^\"]*)\"")
            .Groups[1].Value;

        ActualSegment(secondStepOrder).Should().Contain("display:none",
            "skutečnost kroku 2 (bez data) je absorbována → actual segment skrytý");
        ActualSegment(firstStepOrder).Should().NotContain("display:none")
            .And.Contain("width:", "skutečnost kroku 1 má vykreslený actual segment");
        ActualSegment(thirdStepOrder).Should().NotContain("display:none")
            .And.Contain("width:", "skutečnost kroku 3 má vykreslený actual segment");
    }

    [Fact]
    public async Task Detail_AktualniKrok_MaViditelnySegmentSkutecnosti_DoDneska()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmAktualniOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMAKT");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBAKT", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram aktuální krok record");
        // Vyplněno 1,2 → aktuální krok = 3 (nevyplněný, plán v minulosti → táhne do dneška).
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await (await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}")).Content.ReadAsStringAsync();

        // Overview actual segment kroku 3 (aktuální) musí být viditelný (ne display:none, má width).
        string OverviewActual(int step) =>
            Regex.Match(html, $"data-schedule-segment-kind=\"actual\"\\s+data-step-index=\"{step}\"[\\s\\S]{{0,200}}?style=\"([^\"]*)\"").Groups[1].Value;
        OverviewActual(3).Should().NotContain("display:none", "aktuální krok 3 se kreslí jako rozpracovaný")
            .And.Contain("width:", "segment skutečnosti aktuálního kroku má šířku (táhne do dneška)");
    }

    [Fact]
    public async Task Detail_Harmonogram_RendersMonthTicksAndLocalToday_AndNoPlusOnePxHack()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmTicksOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMTCK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBTCK", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram ticks record");
        // Záznam založený před prvním krokem plánu: krok 1 = [založení, plán kroku 1] má kladnou
        // délku. Se založením „dnes“ by plán kroku 1 (před 23 dny) skončil dřív, než začal.
        var start = DateTime.UtcNow.AddDays(-30);
        await _fixture.SetRecordFoundingDateAsync(recordId, start);
        await _fixture.SeedDatumScheduleAsync(recordId, start, new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await (await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}")).Content.ReadAsStringAsync();

        html.Should().Contain("data-schedule-ticks=", "měsíční ticky se serializují na osu pro JS render");
        html.Should().Contain("data-schedule-today=", "lokální dnešek je v data atributu (sjednocený zdroj markeru s osou)");

        // +1px hack odstraněn — segment plánu má width:NN% bez calc() (sjednocení souřadnic s markery/ticky).
        var planSegmentStyle = Regex.Match(
            html,
            "data-schedule-segment-kind=\"planned\"\\s+data-step-index=\"1\"[\\s\\S]{0,200}?style=\"([^\"]*)\"")
            .Groups[1].Value;
        planSegmentStyle.Should().Contain("width:").And.NotContain("calc(");
    }

    [Fact]
    public async Task Edit_ScheduleTab_PlanDateFields_AreEditable_ForTaskWithEditPermission()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmEditableOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMEDIT");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBEDIT", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram editable plan record");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Regrese (datum-migrace): editor zamykal plán-date inputy přes mrtvé TrvaniTypId gating.
        // Plán je v editoru editovatelný → každé HarmonogramHodnoty[i].PlanDatum pole musí být locked="false".
        var planFields = Regex.Matches(html, "<pm-date-field\\b[^>]*HarmonogramHodnoty\\[\\d+\\]\\.PlanDatum[^>]*>");
        planFields.Count.Should().BeGreaterThan(0, "editor renderuje plán date pole");
        foreach (Match m in planFields)
        {
            m.Value.Should().Contain("locked=\"false\"", "plán je v editoru editovatelný (regrese: TrvaniTypId gating)");
        }
    }

    [Fact]
    public async Task Edit_ScheduleTab_PlanDateField_IsEmpty_WhereNoPlanDate_ButKeepsValueWhereSet()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmEmptyPlanOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMEMPTY");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBEMPTY", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram empty plan record");

        // Jen krok 3 má plánové datum; ostatní kroky žádný řádek (plán = null).
        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = 3,
                PlanDatum = new DateTime(2026, 3, 20),
                SkutecnostDatum = null,
                SkutecnostZdroj = 0,
                SkutecnostRezim = 2,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        // Krok 3 (i=2) má uložené plánové datum → iso-value zůstává vyplněné.
        var step3Field = Regex.Match(html, "<pm-date-field\\b[^>]*HarmonogramHodnoty\\[2\\]\\.PlanDatum[^>]*>");
        step3Field.Success.Should().BeTrue("krok 3 plán pole se renderuje");
        step3Field.Value.Should().Contain("iso-value=\"2026-03-20\"", "vyplněný krok drží svou hodnotu");

        // Krok 1 (i=0) nemá plánové datum → iso-value prázdné (dřív = datum založení = zdroj kaskády).
        var step1Field = Regex.Match(html, "<pm-date-field\\b[^>]*HarmonogramHodnoty\\[0\\]\\.PlanDatum[^>]*>");
        step1Field.Success.Should().BeTrue("krok 1 plán pole se renderuje");
        step1Field.Value.Should().Contain("iso-value=\"\"", "nevyplněný krok má prázdné plánové datum (žádný pre-fill datem založení)");
    }
}
