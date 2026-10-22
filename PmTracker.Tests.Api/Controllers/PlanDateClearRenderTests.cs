using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R13: pole plánu v harmonogramu má ✕ (smazat datum), když ho uživatel smí
/// upravit — v editoru záznamu i v návrhu změny harmonogramu. Read-only tabulka na stránce
/// záznamu ✕ nemá. Krok bez plánu má trvání „–“ (dřív „0 dnů“).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class PlanDateClearRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public PlanDateClearRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Úkol založený před 40 dny; plán kroku k = založení + 3·k, krok <paramref name="bezPlanu"/> bez plánu.</summary>
    private async Task<(int ProjectId, int RecordId)> SeedTaskAsync(string marker, int? bezPlanu = null)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", marker);
        var zalozeni = DateTime.Today.AddDays(-40);
        await _fixture.SetRecordFoundingDateAsync(recordId, zalozeni);

        await using var db = _fixture.CreateDbContext();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = poradi == bezPlanu ? null : zalozeni.AddDays(3 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (projectId, recordId);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static IReadOnlyList<string> PlanFields(string html)
        => Regex.Matches(html, "<pm-date-field\\b[^>]*data-schedule-plan-step=\"\\d+\"[^>]*>")
            .Select(m => m.Value)
            .ToList();

    private static string DurationText(string html, int poradi)
    {
        var row = Regex.Match(html, $"<tr data-schedule-step-row\\s+data-step-index=\"{poradi}\"[\\s\\S]*?</tr>").Value;
        return Regex.Match(row, "data-schedule-duration-readonly[^>]*>\\s*([^<]*?)\\s*</span>").Groups[1].Value;
    }

    [Fact]
    public async Task Editor_PlanFields_HaveClearButton()
    {
        var s = await SeedTaskAsync("APPLANX1");

        var html = await GetHtmlAsync($"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        var fields = PlanFields(html);
        fields.Should().HaveCount(10);
        fields.Should().OnlyContain(f => f.Contains("clearable=\"true\"") && f.Contains("locked=\"false\""),
            "kdo smí plán upravit, smí ho i smazat (R13)");
    }

    [Fact]
    public async Task ScheduleProposalEditor_PlanFields_HaveClearButton()
    {
        var s = await SeedTaskAsync("APPLANX2");

        var html = await GetHtmlAsync(
            $"/Navrhy/CreateScheduleProposal?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        PlanFields(html).Should().HaveCount(10)
            .And.OnlyContain(f => f.Contains("clearable=\"true\""), "odstranění plánu jde navrhnout (R13, R14)");
    }

    [Fact]
    public async Task RecordPageTable_PlanFields_HaveNoClearButton()
    {
        var s = await SeedTaskAsync("APPLANX3");

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        PlanFields(html).Should().HaveCount(10)
            .And.OnlyContain(f => f.Contains("clearable=\"false\"") && f.Contains("locked=\"true\""),
                "tabulka na stránce záznamu je jen pro čtení");
    }

    [Fact]
    public async Task Editor_DurationText_DashWithoutPlan_CzechPlural()
    {
        var s = await SeedTaskAsync("APPLANX4", bezPlanu: 3);

        var html = await GetHtmlAsync($"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        DurationText(html, 1).Should().Be("3 dny");
        DurationText(html, 3).Should().Be("–", "krok bez plánu nemá trvání (dřív „0 dnů“)");
        DurationText(html, 4).Should().Be("6 dnů", "krok 4 začíná koncem kroku 2 — posledního kroku s plánem");
    }
}
