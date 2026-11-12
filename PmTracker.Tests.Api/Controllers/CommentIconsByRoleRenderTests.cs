using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-11-10 R6: tužka a koš u vyjádření se vykreslí přesně tam, kde server úpravu
/// a smazání přijme (příznaky z CommentService.GetCommentActionsAsync). Dřív Gestor v Otevřeno
/// ikony neviděl, ač server povolil, a metodik v Přípravě je viděl, ač server odmítl.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class CommentIconsByRoleRenderTests
{
    private const string Pencil = "data-comment-edit-toggle";
    private const string Trash = "action=\"/Zaznamy/DeleteComment\"";

    private readonly ApiSqlFixture _fixture;

    public CommentIconsByRoleRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(
        int ProjectId, int GestorId, int PmId, int LeadId, int MetodikId,
        int RecordA, int DraftId, int OpenId, int ClosedId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var gestorId = await _fixture.EnsurePersonAsync($"{marker}Gestor");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var metodikId = await _fixture.EnsurePersonAsync($"{marker}Metodik");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemA = await _fixture.EnsureSubsystemAsync($"{marker}A", ownerId);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, gestorId, ProjectRoleCodes.Gestor);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemA, leadId, SubsystemRoleCodes.Lead);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemA, metodikId, SubsystemRoleCodes.Methodik);
        var recordA = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemA, "U", $"{marker} A");
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", meetingBase);
        var openId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase + 1);
        var closedId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", meetingBase + 2);
        return new Scenario(projectId, gestorId, pmId, leadId, metodikId, recordA, draftId, openId, closedId);
    }

    private async Task<string> HtmlAsync(Scenario s, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordA}&loadAll=true&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    /// <summary>Úsek HTML jednoho vyjádření: od jeho data-comment-id po další vyjádření.</summary>
    private static string CommentBlock(string html, int commentId)
    {
        var start = html.IndexOf($"data-comment-id=\"{commentId}\"", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"vyjádření {commentId} musí být v panelu");
        var next = html.IndexOf("data-comment-item", start, StringComparison.Ordinal);
        return next < 0 ? html[start..] : html[start..next];
    }

    private static void Icons(string html, int commentId, bool pencil, bool trash)
    {
        var block = CommentBlock(html, commentId);
        block.Contains(Pencil, StringComparison.Ordinal).Should().Be(pencil, $"tužka u vyjádření {commentId}");
        block.Contains(Trash, StringComparison.Ordinal).Should().Be(trash, $"koš u vyjádření {commentId}");
    }

    [Fact]
    public async Task Gestor_vidi_ikony_u_vlastniho_v_priprave_i_otevrenem()
    {
        var s = await SeedAsync("ApiIco1", 9461);
        var draft = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.GestorId, "g priprava");
        var open = await _fixture.AddCommentAsync(s.RecordA, s.OpenId, s.GestorId, "g otevreno");

        var html = await HtmlAsync(s, s.GestorId);

        Icons(html, draft, pencil: true, trash: true);
        Icons(html, open, pencil: true, trash: true);
    }

    [Fact]
    public async Task Gestor_nevidi_ikony_u_ciziho()
    {
        var s = await SeedAsync("ApiIco2", 9465);
        var pm = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.PmId, "pm");

        Icons(await HtmlAsync(s, s.GestorId), pm, pencil: false, trash: false);
    }

    [Fact]
    public async Task Metodik_vidi_ikony_jen_v_priprave_u_sveho_subsystemu()
    {
        var s = await SeedAsync("ApiIco3", 9469);
        var draft = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.MetodikId, "m priprava");
        var open = await _fixture.AddCommentAsync(s.RecordA, s.OpenId, s.MetodikId, "m otevreno");

        var html = await HtmlAsync(s, s.MetodikId);

        Icons(html, draft, pencil: true, trash: true);
        Icons(html, open, pencil: false, trash: false);
    }

    [Fact]
    public async Task Vedouci_nevidi_ikony_u_vlastniho_v_otevrenem()
    {
        var s = await SeedAsync("ApiIco4", 9473);
        var draft = await _fixture.AddCommentAsync(s.RecordA, s.DraftId, s.LeadId, "v priprava");
        var open = await _fixture.AddCommentAsync(s.RecordA, s.OpenId, s.LeadId, "v otevreno");

        var html = await HtmlAsync(s, s.LeadId);

        Icons(html, draft, pencil: true, trash: true);
        Icons(html, open, pencil: false, trash: false);
    }

    [Fact]
    public async Task Pm_vidi_ikony_u_ciziho_v_otevrenem()
    {
        var s = await SeedAsync("ApiIco5", 9477);
        var gestor = await _fixture.AddCommentAsync(s.RecordA, s.OpenId, s.GestorId, "g");

        Icons(await HtmlAsync(s, s.PmId), gestor, pencil: true, trash: true);
    }

    [Fact]
    public async Task Uzavrene_jednani_bez_ikon_i_pro_pm()
    {
        var s = await SeedAsync("ApiIco6", 9481);
        var closed = await _fixture.AddCommentAsync(s.RecordA, s.ClosedId, s.PmId, "pm uzavreno");

        Icons(await HtmlAsync(s, s.PmId), closed, pencil: false, trash: false);
    }
}
