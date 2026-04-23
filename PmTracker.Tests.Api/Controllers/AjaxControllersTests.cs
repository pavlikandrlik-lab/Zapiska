using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class AjaxControllersTests
{
    private readonly ApiSqlFixture _fixture;

    public AjaxControllersTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveMeeting_ShouldReturnFieldErrors_WhenMeetingTimeIsInvalid()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT1");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("CisloJednani", "123"),
                ("DatumPlanovane", "2026-02-18"),
                ("CasZacatek", "invalid"),
                ("Misto", "Test"),
                ("StavJednani", "DRAFT")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.FieldErrors.Keys.Should().Contain(key => key.Contains("CasZacatek", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EditMeetingModal_ShouldRenderExistingMeetingValues_ForOpenMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT_EDIT_GET");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 126);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/EditMeetingModal?projektId={projectId}&meetingId={meetingId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = System.Net.WebUtility.HtmlDecode(html);
        decodedHtml.Should().Contain("Upravit jednání");
        html.Should().Contain($"name=\"Id\" value=\"{meetingId}\"");
        html.Should().Contain("data-current-meeting-number=\"126\"");
    }

    [Fact]
    public async Task SaveMeeting_ShouldUpdateExistingMeeting_ForOpenMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT_EDIT_POST");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 127);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", meetingId.ToString()),
                ("ProjektId", projectId.ToString()),
                ("CisloJednani", "128"),
                ("DatumPlanovane", "2026-03-04"),
                ("CasZacatek", "13:15"),
                ("Misto", "Upraveno"),
                ("StavJednani", "OPEN")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("projekty-detail-jednani");

        await using var dbContext = _fixture.CreateDbContext();
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.CisloJednani.Should().Be(128);
        meeting.CasZacatek.Should().Be(new TimeOnly(13, 15));
        meeting.Misto.Should().Be("Upraveno");
    }

    [Fact]
    public async Task EditMeetingModal_ShouldReturnForbidden_ForClosedMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT_EDIT_CLOSED");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", 129);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/EditMeetingModal?projektId={projectId}&meetingId={meetingId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteMeeting_ShouldReturnAjaxSuccessAndDeleteMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT2");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 124);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/Delete?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("JednaniId", meetingId.ToString()),
                ("ProjektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("projekty-detail-jednani");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Jednani.AnyAsync(x => x.Id == meetingId)).Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_ShouldReturnAjaxSuccess_ForProjectContext()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIMT3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Api Comment Record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 125);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AddComment?projektId={projectId}&asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString()),
                ("Text", "API comment"),
                ("uiContext", "project")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("record-comments");
        payload.RecordId.Should().Be(recordId);

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Vyjadreni.AnyAsync(x => x.ZaznamId == recordId && x.JednaniId == meetingId)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveRecord_ShouldReturnJsonForbiddenPayload_WhenUserLacksPermission()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiNoPermissionOwner");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiNoPermissionOutsider");
        var projectId = await _fixture.EnsureProjectAsync("APINOPERM");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APINOPERM_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        await using var lookupContext = _fixture.CreateDbContext();
        var categoryCode = await lookupContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == "U" || x.Kod == "UKOL")
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var statusCode = await lookupContext.CiselnikStavuUkolu
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var subsystemCode = await lookupContext.Subsystemy
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .SingleAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("Kategorie", categoryCode),
                ("Stav", statusCode),
                ("Nazev", "No permission record save"),
                ("VlastnikId", ownerId.ToString()),
                ("DatumZalozeni", "2026-06-01"),
                ("TerminUkonceni", "2026-06-15"),
                ("Subsystem", subsystemCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Nemáte oprávnění");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SaveRecord_ShouldReturnCrossTabFieldErrors_WhenValidationFails()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecordOwner");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiRecordOutsider");
        var projectId = await _fixture.EnsureProjectAsync("APIRECVAL");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECVAL_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var hasProjectSubsystem = await dbContext.ProjektSubsystemy
                .AnyAsync(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue);
            if (!hasProjectSubsystem)
            {
                dbContext.ProjektSubsystemy.Add(new ProjektSubsystemEntity
                {
                    ProjektId = projectId,
                    SubsystemId = subsystemId,
                    DatumPrirazeni = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }
        }

        await using var lookupContext = _fixture.CreateDbContext();
        var categoryCode = await lookupContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == "U" || x.Kod == "UKOL")
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var statusCode = await lookupContext.CiselnikStavuUkolu
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var subsystemCode = await lookupContext.Subsystemy
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .SingleAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("Kategorie", categoryCode),
                ("Stav", statusCode),
                ("Nazev", "API record validation"),
                ("Cil", "Test"),
                ("Popis", $"Popis s neplatnym znakem {char.ConvertFromUtf32(1)}"),
                ("VlastnikId", ownerId.ToString()),
                ("DatumZalozeni", "2026-05-05"),
                ("TerminUkonceni", "2026-05-20"),
                ("Subsystem", subsystemCode),
                ("VybraniSpolupracovniciIds", outsiderId.ToString()),
                ("ExterniVazby[0].Typ", "PMP"),
                ("ExterniVazby[0].Cislo", ""),
                ("ExterniVazby[0].PredpokladanaCena", "abc"),
                ("HarmonogramHodnoty[0].TypId", "999999"),
                ("HarmonogramHodnoty[0].Hodnota", "3")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("RECORD_VALIDATION_FAILED");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
        payload.FieldErrors.Keys.Should().Contain("Popis");
        payload.FieldErrors.Keys.Should().Contain("ExterniVazby[0].Cislo");
        payload.FieldErrors.Keys.Should().Contain("ExterniVazby[0].PredpokladanaCena");
        payload.FieldErrors.Keys.Should().Contain("VybraniSpolupracovniciIds");
        payload.FieldErrors.Keys.Should().Contain("HarmonogramHodnoty[0].TypId");
    }

    // SavePermission_ShouldReturnAjaxError_ForUnsupportedKey smazáno: endpoint
    // /Nastaveni/SavePermission byl odstraněn v seed-only RBAC refactoru (commit f1bce3f
    // / 96cd687, 2026-04-22). Role/akce/mapování se mění v PermissionSeedConfiguration.cs
    // přes git/PR, ne přes UI.
}
