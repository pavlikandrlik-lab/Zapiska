using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Po schválení návrhu založení se přesměruje na Záznamy s ?recordId=&lt;nový záznam&gt; (deep-link →
/// sjede + vysvítí), místo návratu na záložku Návrhy. Uživatel hned vidí vzniklý záznam.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProposalApproveFocusRedirectTests
{
    private readonly ApiSqlFixture _fixture;
    public ProposalApproveFocusRedirectTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ApproveCreateProposal_RedirectsToRecordsTab_WithNewRecordId()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiApproveFocusOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPPRFOCUS");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPPRFOCUS_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await using (var seed = _fixture.CreateDbContext())
        {
            if (!await seed.ProjektSubsystemy.AnyAsync(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue))
            {
                seed.ProjektSubsystemy.Add(new ProjektSubsystemEntity { ProjektId = projectId, SubsystemId = subsystemId, DatumPrirazeni = DateTime.UtcNow });
                await seed.SaveChangesAsync();
            }
        }

        await using var lookup = _fixture.CreateDbContext();
        var categoryCode = await lookup.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == "INFO" || x.Nazev == "Informace")
            .OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        var statusCode = await lookup.CiselnikStavuUkolu.OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await lookup.Subsystemy.Where(x => x.Id == subsystemId).Select(x => x.Kod).SingleAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        // 1) Admin (proposals.edit.any) podá návrh založení.
        var submit = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Navrhy/SubmitCreateProposal?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("Kategorie", categoryCode),
                ("Stav", statusCode),
                ("Nazev", "API approve focus"),
                ("VlastnikId", ownerId.ToString()),
                ("DatumZalozeni", "2026-05-05"),
                ("TerminUkonceni", "2026-05-20"),
                ("Subsystem", subsystemCode))));
        var submitPayload = await ApiTestHttpHelper.ReadModalResultAsync(submit);
        submitPayload.Ok.Should().BeTrue(submitPayload.Message);

        int proposalId;
        await using (var db = _fixture.CreateDbContext())
        {
            proposalId = await db.ZaznamNavrhy.AsNoTracking()
                .Where(x => x.ProjektId == projectId)
                .OrderByDescending(x => x.Id).Select(x => x.Id).FirstAsync();
        }

        // 2) Schválení → redirect musí mířit na Záznamy + nový recordId.
        var approve = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Navrhy/ApproveProposal?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ProposalId", proposalId.ToString()))));
        var approvePayload = await ApiTestHttpHelper.ReadModalResultAsync(approve);

        approvePayload.Ok.Should().BeTrue(approvePayload.Message);
        approvePayload.RecordId.Should().HaveValue();
        approvePayload.Tab.Should().Be("zaznamy", "po schválení se skáče na Záznamy (ne zpět na Návrhy)");
        approvePayload.RefreshUrl.Should().Contain($"recordId={approvePayload.RecordId!.Value}",
            "redirect musí nést nový recordId → deep-link sjede a vysvítí vzniklý záznam");
    }
}
