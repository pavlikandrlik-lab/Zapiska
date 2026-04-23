using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
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
    // Fáze 2E: <gov-dialog data-modal-variant="record-editor"> nahradil custom .modal-overlay
    // markup (viz Views/Shared/_ModalLayout.cshtml). Test teď kontroluje gov-dialog marker.
    [InlineData(null, true, "modal", "data-modal-variant=\"record-editor\"")]
    [InlineData(null, false, "page", "record-editor-page-shell")]
    [InlineData("page", true, "page", "record-editor-page-shell")]
    [InlineData("modal", false, "modal", "data-modal-variant=\"record-editor\"")]
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
        Regex.IsMatch(
            html,
            "<textarea[^>]*name=\"Cil\"[^>]*maxlength=\"500\"[^>]*data-record-goal-autogrow=\"true\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeTrue("editor musí renderovat Cíl jako auto-grow textarea s limitem 500 znaků");
        Regex.IsMatch(
            html,
            "<textarea[^>]*name=\"Popis\"[^>]*data-rich-text=\"true\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeTrue("editor musí renderovat Popis jako rich text textarea");
    }

    [Fact]
    public async Task Edit_ShouldExposeEditableCreatedDateField()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiEditCreatedDateOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDDATE");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDDATESUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API created date record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Regex.IsMatch(
                html,
                "Datum založení[\\s\\S]*?data-app-date-locked=\"false\"",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeTrue("datum založení má být při editaci normálně odemčené");
    }

    [Fact]
    public async Task RecordCardPartial_ShouldRenderGoalInSubtitle_WithoutEagerDescription()
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
        html.Should().NotContain("<span class=\"label\">Popis:</span>");
        html.Should().NotContain("Detailni popis po rozbaleni");
    }

    [Fact]
    public async Task RecordDetailPartial_ShouldRenderRichDescriptionAsHtml()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCardRichOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDRICH");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDRICHSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API card rich record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
            record.Popis = "<p><strong>Bold</strong> a <a href=\"https://example.com\">odkaz</a></p>";
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordDetailPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("<span class=\"label\">Popis:</span>");
        html.Should().Contain("<strong>Bold</strong>");
        html.Should().Contain("href=\"https://example.com/\"");
        html.Should().NotContain("&lt;strong&gt;Bold&lt;/strong&gt;");
    }

    [Fact]
    public async Task RecordCommentsPartial_ShouldRenderDefaultFiveNewestComments()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentsOrderOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCOMMENTORD");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCOMMENTSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API comment ordering record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 4201);

        await SeedRecordCommentsAsync(
            recordId,
            ownerId,
            meetingId,
            Enumerable.Range(1, 7)
                .Select(index => ($"Comment {index}", new DateTime(2026, 3, index, 9, 0, 0, DateTimeKind.Utc))));

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Regex.Matches(html, "data-comment-item").Count.Should().Be(5, html);
        html.Should().Contain("Comment 7");
        html.Should().Contain("Comment 6");
        html.Should().Contain("Comment 5");
        html.Should().Contain("Comment 4");
        html.Should().Contain("Comment 3");
        html.Should().NotContain("<p>Comment 2</p>");
        html.Should().NotContain("<p>Comment 1</p>");
        html.Should().Contain("data-record-comments-loaded-count=\"5\"");
        html.Should().Contain("data-record-comments-total-count=\"7\"");
        // Load-more button: šablona renderuje title „Zobrazit N dalších vyjádření" +
        // vlastní label „Další (N)" (viz _ZaznamCommentsPartial.cshtml).
        html.Should().Contain("data-record-comments-load-more=\"true\"");
        html.Should().Contain("Zobrazit vše");
    }

    [Fact]
    public async Task RecordCommentsPartial_WithExplicitLimit_ShouldRenderRequestedSlice()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentsLimitOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCOMMENTLIM");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCOMMENTLIMSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API comment limit record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 4202);

        await SeedRecordCommentsAsync(
            recordId,
            ownerId,
            meetingId,
            Enumerable.Range(1, 12)
                .Select(index => ($"Limit comment {index}", new DateTime(2026, 4, index, 9, 0, 0, DateTimeKind.Utc))));

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&limit=10&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Regex.Matches(html, "data-comment-item").Count.Should().Be(10, html);
        html.Should().Contain("Limit comment 12");
        html.Should().Contain("Limit comment 3");
        html.Should().NotContain("<p>Limit comment 2</p>");
        html.Should().NotContain("<p>Limit comment 1</p>");
        html.Should().Contain("data-record-comments-loaded-count=\"10\"");
        html.Should().Contain("data-record-comments-total-count=\"12\"");
        html.Should().Contain("data-record-comments-load-more=\"true\"");
        html.Should().Contain("Zobrazit vše");
    }

    [Fact]
    public async Task RecordCommentsPartial_WithLoadAll_ShouldRenderAllComments()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentsAllOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCOMMENTALL");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCOMMENTALLSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API comment load-all record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 4203);

        await SeedRecordCommentsAsync(
            recordId,
            ownerId,
            meetingId,
            Enumerable.Range(1, 6)
                .Select(index => ($"All comment {index}", new DateTime(2026, 5, index, 9, 0, 0, DateTimeKind.Utc))));

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&loadAll=true&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Regex.Matches(html, "data-comment-item").Count.Should().Be(6, html);
        html.Should().Contain("All comment 6");
        html.Should().Contain("All comment 1");
        html.Should().Contain("data-record-comments-loaded-count=\"6\"");
        html.Should().Contain("data-record-comments-total-count=\"6\"");
        html.Should().Contain("data-record-comments-is-fully-loaded=\"true\"");
        html.Should().NotContain("Zobrazit dalších 5");
        html.Should().NotContain("Zobrazit vše");
    }

    [Fact]
    public async Task RecordCommentsPartial_ShouldOfferOnlyDraftMeetings_ForSubsystemLeadWithoutRecordsEdit()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentsDraftOnlyOwner");
        var leadUserId = await _fixture.EnsurePersonAsync("ApiCommentsDraftOnlyLead");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCOMMDRAFT");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCOMMDRAFTSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API comments draft-only record");
        var draftMeetingId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", 4204);
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 4205);

        await AssignSubsystemRoleAsync(projectId, subsystemId, leadUserId, SubsystemRoleCodes.Lead);
        await GrantProjectPermissionAsync(projectId, leadUserId, PermissionKeys.MeetingsNotesSubsystemLead, "ApiCommentsDraftOnly");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={leadUserId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("comment-form");
        html.Should().Contain($"<option value=\"{draftMeetingId}\">");
        html.Should().NotContain($"<option value=\"{openMeetingId}\">");
    }

    [Fact]
    public async Task RecordCommentsPartial_ShouldHideAddForm_WhenSubsystemLeadHasOnlyOpenMeetings()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentsOpenOnlyOwner");
        var leadUserId = await _fixture.EnsurePersonAsync("ApiCommentsOpenOnlyLead");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCOMMOPEN");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCOMMOPENSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API comments open-only record");
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 4206);

        await AssignSubsystemRoleAsync(projectId, subsystemId, leadUserId, SubsystemRoleCodes.Lead);
        await GrantProjectPermissionAsync(projectId, leadUserId, PermissionKeys.MeetingsNotesSubsystemLead, "ApiCommentsOpenOnly");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={leadUserId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("comment-form");
        html.Should().NotContain($"<option value=\"{openMeetingId}\">");
    }

    [Fact]
    public async Task RecordCommentsPartial_ShouldKeepOpenMeetingsVisible_ForRecordsEditUser()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentsEditorOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCOMMEDITOR");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDCOMMEDITORSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API comments editor record");
        var draftMeetingId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", 4207);
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 4208);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("comment-form");
        html.Should().Contain($"<option value=\"{draftMeetingId}\">");
        html.Should().Contain($"<option value=\"{openMeetingId}\">");
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
    public async Task Save_ShouldPersistUpdatedCreatedDate_ForExistingRecord()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiEditCreatedDateSaveOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDDATE2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDDATE2SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API save created date record");

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
        var newCreatedDate = record.DatumUkonceni.Date.AddDays(-1);

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
                ("DatumZalozeni", newCreatedDate.ToString("yyyy-MM-dd")),
                ("TerminUkonceni", record.DatumUkonceni.ToString("yyyy-MM-dd")),
                ("Subsystem", subsystemCode),
                ("CisloZaznamu", record.CisloZaznamu.ToString()),
                ("EditorTab", "basic"),
                ("Presentation", "modal")));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        (await ApiTestHttpHelper.ReadModalResultAsync(response)).Ok.Should().BeTrue();

        await using var verificationDbContext = _fixture.CreateDbContext();
        var savedCreatedDate = await verificationDbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => x.DatumZalozeni)
            .FirstAsync();
        savedCreatedDate.Date.Should().Be(newCreatedDate.Date);
    }

    [Fact]
    public async Task Save_ShouldPersistGoalWithFiveHundredCharacters_AndLineBreaks()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiLongGoalOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDGOAL500");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDGOALSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API save long goal");

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

        var firstLine = new string('A', 249);
        var secondLine = new string('B', 250);
        var goal = $"{firstLine}\n{secondLine}";
        goal.Length.Should().Be(500);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", record.Id.ToString()),
                ("ProjektId", record.ProjektId.ToString()),
                ("Kategorie", categoryName),
                ("Stav", statusName),
                ("Nazev", $"{record.Nazev} updated"),
                ("Cil", goal),
                ("Popis", record.Popis ?? string.Empty),
                ("VlastnikId", record.VlastnikId.ToString()),
                ("DatumZalozeni", record.DatumZalozeni.ToString("yyyy-MM-dd")),
                ("TerminUkonceni", record.DatumUkonceni.ToString("yyyy-MM-dd")),
                ("Subsystem", subsystemCode),
                ("CisloZaznamu", record.CisloZaznamu.ToString()),
                ("EditorTab", "basic"),
                ("Presentation", "modal")));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();

        await using var verificationDbContext = _fixture.CreateDbContext();
        var savedGoal = await verificationDbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => x.Cil)
            .SingleAsync();
        savedGoal.Should().Be(goal);
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
        Regex.Matches(html, "data-schedule-gantt-today").Count.Should().Be(2);
        html.Should().Contain("Plán / Skutečnost / Dnes / Termín");
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
    public async Task Create_ShouldPrefillMeetingContextAndStartDate_WhenOpenedFromMeetingDetail()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCreateMeetingCtxOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDCMEET");
        await _fixture.EnsureSubsystemAsync("APIREDCMEETSUB", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9850);
        var meetingDate = new DateTime(2026, 8, 21);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
            project.PouzivatIdentJednani = true;
            var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == meetingId);
            meeting.DatumPlanovane = meetingDate;
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Create?projektId={projectId}&jednaniId={meetingId}&uiContext=meeting&asUser={_fixture.AdminOsobaId}&presentation=modal");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain($"name=\"MeetingId\" value=\"{meetingId}\"");
        html.Should().Contain("name=\"UiContext\" value=\"meeting\"");
        html.Should().Contain("name=\"JednaniIdProCislo\"");
        html.Should().Contain($"data-record-meeting-date=\"{meetingDate:yyyy-MM-dd}\"");
        html.Should().Contain($"name=\"DatumZalozeni\" value=\"{meetingDate:yyyy-MM-dd}\"");
    }

    [Fact]
    public async Task Save_ShouldReturnMeetingDetailRefresh_WhenCreatingFromMeetingContext()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiSaveMeetingCtxOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIREDSMCTX");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIREDSMCTXSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9851);
        var meetingDate = new DateTime(2026, 9, 2);

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
            }

            var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == meetingId);
            meeting.DatumPlanovane = meetingDate;
            await dbContext.SaveChangesAsync();
        }

        await using var dbContextForLookup = _fixture.CreateDbContext();
        var categoryName = await dbContextForLookup.CiselnikKategoriiZaznamu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Nazev)
            .FirstAsync();
        var statusName = await dbContextForLookup.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Nazev)
            .FirstAsync();
        var subsystemCode = await dbContextForLookup.Subsystemy.AsNoTracking()
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .FirstAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/Save?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("Kategorie", categoryName),
                ("Stav", statusName),
                ("Nazev", "Meeting-context create"),
                ("Cil", "Meeting-context create"),
                ("Popis", "<p>Meeting-context create</p>"),
                ("VlastnikId", ownerId.ToString()),
                ("DatumZalozeni", meetingDate.ToString("yyyy-MM-dd")),
                ("TerminUkonceni", meetingDate.AddDays(10).ToString("yyyy-MM-dd")),
                ("Subsystem", subsystemCode),
                ("CisloZaznamu", "0"),
                ("EditorTab", "basic"),
                ("Presentation", "modal"),
                ("UiContext", "meeting"),
                ("MeetingId", meetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("page");
        payload.UiContext.Should().Be("meeting");
        payload.MeetingId.Should().Be(meetingId);
        payload.RefreshUrl.Should().Contain($"/Jednani/Detail/{meetingId}");
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
            // ZaznamHistorieStavuProjektu FKuje na ciselnik_stavu_projektu (NE na ciselnik_stavu_ukolu
            // jako zbytek tohoto bloku). Dřívější použití statusId → FK violation.
            var projectStatusId = await dbContext.CiselnikStavuProjektu
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync();

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
                PuvodniStav = projectStatusId,
                NovyStav = projectStatusId,
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
            x.EntityType == "zaznam" &&
            x.EntityId == recordId.ToString() &&
            x.Action == "delete");
        auditExists.Should().BeTrue();
    }

    private async Task SeedRecordCommentsAsync(
        int recordId,
        int authorId,
        int meetingId,
        IEnumerable<(string Text, DateTime Datum)> comments)
    {
        await using var dbContext = _fixture.CreateDbContext();
        foreach (var (text, datum) in comments)
        {
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = authorId,
                TextVyjadreni = text,
                DatumVyjadreni = datum
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task AssignSubsystemRoleAsync(int projectId, int subsystemId, int osobaId, string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var roleId = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        roleId.Should().NotBeNull();

        var projectSubsystemId = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        projectSubsystemId.Should().NotBeNull();

        var existing = await dbContext.ObsazeniSubsystemuProjektu
            .FirstOrDefaultAsync(x =>
                x.ProjektSubsystemId == projectSubsystemId!.Value &&
                x.OsobaId == osobaId &&
                x.RoleSubsystemuId == roleId!.Value &&
                !x.DatumOdebrani.HasValue);
        if (existing is not null)
        {
            return;
        }

        dbContext.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            ProjektSubsystemId = projectSubsystemId.Value,
            OsobaId = osobaId,
            RoleSubsystemuId = roleId.Value,
            DatumPrirazeni = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task GrantProjectPermissionAsync(int projectId, int osobaId, string permissionKey, string roleMarker)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var permissionId = await dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => x.IsActive && x.Klic == permissionKey)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        permissionId.Should().NotBeNull();

        var roleCode = $"API_{roleMarker}_{Guid.NewGuid():N}".ToUpperInvariant();
        roleCode = roleCode.Length <= 40 ? roleCode : roleCode[..40];

        var role = new AuthzRoleEntity
        {
            Kod = roleCode,
            Nazev = roleMarker,
            Popis = roleMarker,
            IsSystem = false,
            IsActive = true
        };
        dbContext.AuthzRoles.Add(role);
        await dbContext.SaveChangesAsync();

        var rolePermission = new AuthzRolePermissionEntity
        {
            RoleId = role.Id,
            PermissionId = permissionId.Value,
            ScopeMode = ScopeMode.Include,
            IsAllowed = true
        };
        dbContext.AuthzRolePermissions.Add(rolePermission);
        await dbContext.SaveChangesAsync();

        dbContext.AuthzRolePermissionProjects.Add(new AuthzRolePermissionProjectEntity
        {
            RolePermissionId = rolePermission.Id,
            ProjektId = projectId
        });

        dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
        {
            OsobaId = osobaId,
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }
}
