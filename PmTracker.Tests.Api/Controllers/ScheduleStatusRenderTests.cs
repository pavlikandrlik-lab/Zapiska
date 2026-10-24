using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-24: záznam bez plánu se skutečností z automatu — stav se počítá k celkovému
/// termínu a říká to („plán není zadán · celkový termín…“), plán dokončení „není zadán“, trvání „–“,
/// krok bez plánu v Rozpadu „bez plánu“, štítek podle termínu. Krok s plánem dál k plánu, se skloňováním.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleStatusRenderTests
{
    private const string Krok2 = "2. konzultace terminu s dodavatelem pred vytvorenim zadani";
    private readonly ApiSqlFixture _fixture;

    public ScheduleStatusRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Úkol založený před 100 dny s termínem za <paramref name="terminZaDni"/> dní a zadanými kroky.</summary>
    private async Task<(int ProjectId, int RecordId, DateTime Zalozeni)> SeedAsync(
        string marker, int terminZaDni, Func<int, DateTime, (DateTime? Plan, DateTime? Skutecnost)> krok)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", marker);
        var zalozeni = DateTime.Today.AddDays(-100);
        await _fixture.SetRecordFoundingDateAsync(recordId, zalozeni);

        await using var db = _fixture.CreateDbContext();
        await db.ProjektoveZaznamy.Where(x => x.Id == recordId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DatumUkonceni, DateTime.Today.AddDays(terminZaDni)));
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            var (plan, skutecnost) = krok(poradi, zalozeni);
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = plan,
                SkutecnostDatum = skutecnost,
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = skutecnost.HasValue
                    ? (byte)SkutecnostZdrojEnum.Automat
                    : (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (projectId, recordId, zalozeni);
    }

    /// <summary>Uživatelův případ: žádný plán, skutečnost kroků 1/3/4 z automatu → aktuální krok 5.</summary>
    private static (DateTime?, DateTime?) BezPlanuAutomat(int poradi, DateTime zalozeni) => poradi switch
    {
        1 => (null, zalozeni.AddDays(2)),
        3 => (null, zalozeni.AddDays(20)),
        4 => (null, zalozeni.AddDays(60)),
        _ => (null, null)
    };

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Text(string html, string attribute)
        => Regex.Match(html, $"{attribute}>([^<]*)<").Groups[1].Value.Trim();

    private static (string Color, string Text) Tag(string html)
    {
        var m = Regex.Match(html, "<gov-tag color=\"(\\w+)\" size=\"s\">(Stíháme|Nestíháme)</gov-tag>");
        return (m.Groups[1].Value, m.Groups[2].Value);
    }

    private static (string Css, string Text) Offset(string html, int poradi)
    {
        var m = Regex.Match(html,
            $"<div class=\"gantt-step-row\"[^>]*data-step-index=\"{poradi}\"[^>]*>[\\s\\S]*?<span class=\"gantt-step-offset\\s*([a-z]*)\\s*\" data-schedule-offset>([^<]*)</span>");
        m.Success.Should().BeTrue($"krok {poradi} má mít v Rozpadu odchylku");
        return (m.Groups[1].Value, m.Groups[2].Value.Trim());
    }

    [Fact]
    public async Task RecordPage_WithoutPlan_StateCountsToDeadline_PlanNotSet_StepWithoutPlan()
    {
        var s = await SeedAsync("APSTAV1", terminZaDni: 12, BezPlanuAutomat);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should()
            .Be("Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní");
        Text(html, "data-schedule-summary-baseline").Should().Be("není zadán");
        Offset(html, 1).Should().Be((string.Empty, "bez plánu"), "odchylka od dopočteného plánu by byla nesmysl");
        Tag(html).Should().Be(("success", "Stíháme"), "do celkového termínu zbývá 12 dní");
    }

    [Fact]
    public async Task Editor_WithoutPlan_PlanCompletionNotSet_DurationDash()
    {
        var s = await SeedAsync("APSTAV2", terminZaDni: 12, BezPlanuAutomat);

        var html = await GetHtmlAsync($"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should()
            .Be("Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: zbývá 12 dní");
        Text(html, "data-schedule-summary-baseline").Should().Be("není zadán");
        Text(html, "data-schedule-summary-duration").Should().Be("–");
    }

    [Fact]
    public async Task ScheduleTab_WithoutPlan_PastDeadline_StateAndTagByDeadline()
    {
        var s = await SeedAsync("APSTAV3", terminZaDni: -10, BezPlanuAutomat);

        var html = await GetHtmlAsync($"/Projekty/HarmonogramTabPartial/{s.ProjectId}?asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should()
            .Be("Aktuální krok: 5. vyporadani pripominek · plán není zadán · celkový termín: skluz +10 dní");
        Tag(html).Should().Be(("error", "Nestíháme"));
    }

    [Fact]
    public async Task RecordPage_CurrentStepWithPlan_CountsToPlan_CzechPlural()
    {
        // Krok 1 plán + skutečnost o den později; krok 2 (aktuální) plán včera.
        var s = await SeedAsync("APSTAV4", terminZaDni: 30, (poradi, zalozeni) => poradi switch
        {
            1 => (zalozeni.AddDays(10), zalozeni.AddDays(11)),
            2 => (DateTime.Today.AddDays(-1), null),
            _ => (null, null)
        });

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        Text(html, "data-schedule-summary-state").Should().Be($"Aktuální krok: {Krok2} · skluz +1 den");
        Text(html, "data-schedule-summary-baseline").Should().Be(DateTime.Today.AddDays(-1).ToString("dd.MM.yyyy"));
        Offset(html, 1).Should().Be(("late", "+1 den"));
        Tag(html).Should().Be(("error", "Nestíháme"));
    }
}
