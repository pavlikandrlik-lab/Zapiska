using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-11-10 (vyjádření podle role) — server: přidání, úprava a smazání podvrženým požadavkem.
/// Gestor upravuje a maže vlastní v Přípravě i Otevřeno; metodik, vedoucí a zástupce jen v Přípravě
/// a jen u svého subsystému; nabídka jednání ve formuláři se počítá touž metodou.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class CommentRoleMatrixServerTests
{
    private readonly ApiSqlFixture _fixture;

    public CommentRoleMatrixServerTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(
        int ProjectId, int GestorId, int PmId, int LeadId, int MetodikId,
        int RecordA, int RecordB, int DraftId, int OpenId);

    /// <summary>Záznam A v subsystému A (vedoucí a metodik), záznam B v cizím subsystému B; jednání v Přípravě a Otevřeno.</summary>
    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var gestorId = await _fixture.EnsurePersonAsync($"{marker}Gestor");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var metodikId = await _fixture.EnsurePersonAsync($"{marker}Metodik");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemA = await _fixture.EnsureSubsystemAsync($"{marker}A", ownerId);
        var subsystemB = await _fixture.EnsureSubsystemAsync($"{marker}B", ownerId);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, gestorId, ProjectRoleCodes.Gestor);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemA, leadId, SubsystemRoleCodes.Lead);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemA, metodikId, SubsystemRoleCodes.Methodik);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemB, ownerId, SubsystemRoleCodes.Lead);
        var recordA = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemA, "U", $"{marker} A");
        var recordB = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemB, "U", $"{marker} B");
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", meetingBase);
        var openId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase + 1);
        return new Scenario(projectId, gestorId, pmId, leadId, metodikId, recordA, recordB, draftId, openId);
    }

    private async Task<bool> PostAsync(string action, int asUser, params (string Key, string Value)[] fields)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost($"/Zaznamy/{action}?asUser={asUser}", ApiTestHttpHelper.BuildForm(fields));
        return (await ApiTestHttpHelper.ReadModalResultAsync(await client.SendAsync(request))).Ok;
    }

    private Task<bool> AddAsync(Scenario s, int asUser, int recordId, int meetingId, string text)
        => PostAsync("AddComment", asUser,
            ("projektId", s.ProjectId.ToString()), ("uiContext", "project"),
            ("ZaznamId", recordId.ToString()), ("JednaniId", meetingId.ToString()), ("Text", $"<p>{text}</p>"));

    private Task<bool> UpdateAsync(Scenario s, int asUser, int recordId, int commentId, string text)
        => PostAsync("UpdateComment", asUser,
            ("projektId", s.ProjectId.ToString()), ("uiContext", "project"),
            ("zaznamId", recordId.ToString()), ("Id", commentId.ToString()), ("Text", $"<p>{text}</p>"));

    private Task<bool> DeleteAsync(Scenario s, int asUser, int recordId, int commentId)
        => PostAsync("DeleteComment", asUser,
            ("projektId", s.ProjectId.ToString()), ("uiContext", "project"),
            ("zaznamId", recordId.ToString()), ("Id", commentId.ToString()));

    private async Task<string?> CommentTextAsync(int commentId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Vyjadreni.AsNoTracking().Where(x => x.Id == commentId).Select(x => x.TextVyjadreni).SingleOrDefaultAsync();
    }

    private async Task<int> CommentCountAsync(int recordId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Vyjadreni.CountAsync(x => x.ZaznamId == recordId);
    }

    [Fact]
    public async Task Gestor_upravi_vlastni_v_priprave()
    {
        var s = await SeedAsync("ApiCrm1", 9431);
        var id = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.GestorId, "puvodni");

        (await UpdateAsync(s, s.GestorId, s.RecordA, id, "upraveno")).Should().BeTrue();
        (await CommentTextAsync(id)).Should().Contain("upraveno");
    }

    [Fact]
    public async Task Gestor_smaze_vlastni_v_priprave()
    {
        var s = await SeedAsync("ApiCrm2", 9433);
        var id = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.GestorId, "ke smazani");

        (await DeleteAsync(s, s.GestorId, s.RecordA, id)).Should().BeTrue();
        (await CommentTextAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Gestor_upravi_a_smaze_vlastni_v_otevrenem()
    {
        var s = await SeedAsync("ApiCrm3", 9435);
        var id = await _fixture.AddCommentAsync(s.RecordB, s.OpenId, s.GestorId, "puvodni");

        (await UpdateAsync(s, s.GestorId, s.RecordB, id, "upraveno")).Should().BeTrue();
        (await DeleteAsync(s, s.GestorId, s.RecordB, id)).Should().BeTrue();
        (await CommentTextAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Gestor_neupravi_cizi()
    {
        var s = await SeedAsync("ApiCrm4", 9437);
        var id = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.PmId, "pm text");

        (await UpdateAsync(s, s.GestorId, s.RecordA, id, "podvrh")).Should().BeFalse();
        (await CommentTextAsync(id)).Should().Be("pm text");
    }

    [Fact]
    public async Task Metodik_prida_upravi_smaze_v_priprave_u_sveho_subsystemu()
    {
        var s = await SeedAsync("ApiCrm5", 9439);

        (await AddAsync(s, s.MetodikId, s.RecordA, s.DraftId, "metodik")).Should().BeTrue();
        await using var db = _fixture.CreateDbContext();
        var id = await db.Vyjadreni.Where(x => x.ZaznamId == s.RecordA && x.AutorOsobaId == s.MetodikId).Select(x => x.Id).SingleAsync();
        (await UpdateAsync(s, s.MetodikId, s.RecordA, id, "metodik upravil")).Should().BeTrue();
        (await DeleteAsync(s, s.MetodikId, s.RecordA, id)).Should().BeTrue();
        (await CommentTextAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Metodik_neprida_v_otevrenem()
    {
        var s = await SeedAsync("ApiCrm6", 9441);

        (await AddAsync(s, s.MetodikId, s.RecordA, s.OpenId, "metodik")).Should().BeFalse();
        (await CommentCountAsync(s.RecordA)).Should().Be(0);
    }

    [Fact]
    public async Task Metodik_neprida_u_ciziho_subsystemu()
    {
        var s = await SeedAsync("ApiCrm7", 9443);

        (await AddAsync(s, s.MetodikId, s.RecordB, s.DraftId, "metodik")).Should().BeFalse();
        (await CommentCountAsync(s.RecordB)).Should().Be(0);
    }

    [Fact]
    public async Task Metodik_neupravi_ani_nesmaze_starsi_vlastni_v_otevrenem()
    {
        var s = await SeedAsync("ApiCrm8", 9445);
        var id = await _fixture.AddCommentAsync(s.RecordA, s.OpenId, s.MetodikId, "starsi");

        (await UpdateAsync(s, s.MetodikId, s.RecordA, id, "podvrh")).Should().BeFalse();
        (await DeleteAsync(s, s.MetodikId, s.RecordA, id)).Should().BeFalse();
        (await CommentTextAsync(id)).Should().Be("starsi");
    }

    [Fact]
    public async Task Vedouci_neupravi_ani_nesmaze_vlastni_v_otevrenem()
    {
        var s = await SeedAsync("ApiCrm9", 9447);
        var id = await _fixture.AddCommentAsync(s.RecordA, s.OpenId, s.LeadId, "vedouci");

        (await UpdateAsync(s, s.LeadId, s.RecordA, id, "podvrh")).Should().BeFalse();
        (await DeleteAsync(s, s.LeadId, s.RecordA, id)).Should().BeFalse();
        (await CommentTextAsync(id)).Should().Be("vedouci");
    }

    [Fact]
    public async Task Metodik_nabidka_jednani_jen_priprava_u_sveho_subsystemu()
    {
        var s = await SeedAsync("ApiCrm10", 9449);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var ownResponse = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordA}&asUser={s.MetodikId}");
        var own = await ownResponse.Content.ReadAsStringAsync();
        ownResponse.StatusCode.Should().Be(HttpStatusCode.OK, own);
        own.Should().Contain($"<option value=\"{s.DraftId}\"");
        own.Should().NotContain($"<option value=\"{s.OpenId}\"", "metodik v Otevřeno nepřidává");

        var foreignResponse = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordB}&asUser={s.MetodikId}");
        var foreign = await foreignResponse.Content.ReadAsStringAsync();
        foreignResponse.StatusCode.Should().Be(HttpStatusCode.OK, foreign);
        foreign.Should().NotContain("action=\"/Zaznamy/AddComment\"", "u cizího subsystému metodik nepřidává");
    }
}
