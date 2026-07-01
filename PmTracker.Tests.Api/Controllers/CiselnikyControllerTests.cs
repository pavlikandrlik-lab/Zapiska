using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class CiselnikyControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public CiselnikyControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Panel_ShouldRenderDictionaryDetail()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Ciselniky/Panel?id=typy-ukolu&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Nadpis "Klíč: <key>" byl záměrně odstraněn (commit 6b4ed1d); klíč číselníku nese
        // hidden input v každém řádku/formuláři.
        decodedHtml.Should().Contain("name=\"Key\" value=\"typy-ukolu\"");
        decodedHtml.Should().Contain("Nová položka");
    }

    [Fact]
    public async Task EditRow_ShouldReturnForbidden_WhenUserLacksDictionaryPermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiDictNoPermission");
        var rowId = await GetUnlockedTaskTypeRowIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/EditRow?key=typy-ukolu&id={rowId}&asUser={userId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditRow_ShouldReturnForbidden_ForScheduleDictionary_WhenUserIsNotSuperAdmin()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictNoSuperadminHarmonogram");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/EditRow?key=harmonogram-kroky&id=1&asUser={editorId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditRow_ShouldReturnForbidden_ForLockedRow_WhenUserIsNotSuperAdmin()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictNoSuperadminLockedRow");
        var rowId = await GetLockedTaskStatusRowIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/EditRow?key=stavy-ukolu&id={rowId}&asUser={editorId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditRow_ShouldReturnNotFound_WhenRowDoesNotExist()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorMissingRow");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/EditRow?key=typy-ukolu&id=99999999&asUser={editorId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditRow_ShouldRenderModal_ForLockedRow_WhenUserIsSuperAdmin()
    {
        var rowId = await GetLockedTaskStatusRowIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/EditRow?key=stavy-ukolu&id={rowId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("Upravit položku");
        html.Should().Contain($@"name=""Id"" value=""{rowId}""");
        html.Should().Contain(@"name=""Key"" value=""stavy-ukolu""");
    }

    [Fact]
    public async Task EditRow_ShouldRenderModal_ForUnlockedRow_WhenUserHasPermission()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorAllowed");
        var rowId = await GetUnlockedTaskTypeRowIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/EditRow?key=typy-ukolu&id={rowId}&asUser={editorId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("Upravit položku");
        html.Should().Contain($"name=\"Id\" value=\"{rowId}\"");
        html.Should().Contain("name=\"Key\" value=\"typy-ukolu\"");
    }

    [Fact]
    public async Task SaveRow_ShouldReturnValidationPayload_WhenModelIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/SaveRow?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "typy-ukolu"),
                ("Nazev", "Nevalidní test")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("REQUEST_VALIDATION_FAILED");
        payload.Message.Should().Contain("Položku číselníku nelze uložit");
    }

    [Fact]
    public async Task SaveRow_ShouldRedirectWithTempData_WhenModelIsInvalid_ForNonAjaxRequest()
    {
        // Test simuluje non-AJAX submit: POST → redirect → GET indexu. TestAuthHandler
        // autentizuje na základě ?asUser=, takže follow-up GET musí ?asUser= nést explicitně
        // (redirect jej nepřenáší). Používáme pattern jako v PeopleControllerTests.
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Ciselniky/SaveRow?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("Key", "typy-ukolu"),
                ("Nazev", "Nevalidní non-ajax test"))
        };

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var indexResponse = await client.GetAsync($"/Ciselniky?id=typy-ukolu&asUser={_fixture.AdminOsobaId}");
        var html = await indexResponse.Content.ReadAsStringAsync();

        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // _Layout.cshtml teď renderuje TempData["ErrorMessage"] přes <gov-message color="error">.
        html.Should().Contain("<gov-message color=\"error\">");
        html.Should().Contain("The Kod field is required.");
    }

    [Fact]
    public async Task SaveRow_ShouldNormalizeLockState_ForNonSuperAdminEditor()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorNormalizeLock");
        var code = $"API_DICT_{Guid.NewGuid():N}"[..17];

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/SaveRow?asUser={editorId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "typy-ukolu"),
                ("Kod", code),
                ("Nazev", "API lock normalization"),
                ("IsLocked", "true")));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("ciselniky-detail");
        payload.CiselnikKey.Should().Be("typy-ukolu");

        await using var dbContext = _fixture.CreateDbContext();
        var saved = await dbContext.CiselnikTypuUkolu
            .AsNoTracking()
            .SingleAsync(x => x.Kod == code);

        saved.IsLocked.Should().BeFalse("běžný editor nesmí uzamknout položku");
    }

    [Fact]
    public async Task SaveRow_ShouldReturnForbiddenPayload_ForLockedRow_WhenUserIsNotSuperAdmin()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorLockedSave");

        await using var setupDbContext = _fixture.CreateDbContext();
        var lockedRow = await setupDbContext.CiselnikStavuUkolu
            .AsNoTracking()
            .Where(x => x.IsLocked)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .FirstAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/SaveRow?asUser={editorId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "stavy-ukolu"),
                ("Id", lockedRow.Id.ToString()),
                ("Kod", lockedRow.Kod),
                ("Nazev", $"{lockedRow.Nazev} blocked")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Nemáte oprávnění");
    }

    [Fact]
    public async Task SaveRow_ShouldReturnOperationError_WhenUpdatingMissingRow()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/SaveRow?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "typy-ukolu"),
                ("Id", "99999999"),
                ("Kod", "MISSING_ROW"),
                ("Nazev", "Missing row update")));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("nebyl nalezen");
    }

    [Fact]
    public async Task SaveRow_ShouldReturnForbiddenPayload_ForSuperAdminOnlyDictionary_WhenUserIsNotSuperAdmin()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorHarmonogram");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/SaveRow?asUser={editorId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "harmonogram-kroky"),
                ("Kod", "STEP_API"),
                ("Nazev", "Neplatný pokus")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Nemáte oprávnění");
    }

    [Fact]
    public async Task DeleteRow_ShouldDeleteUnlockedRow_AndRedirect()
    {
        var code = $"API_DEL_{Guid.NewGuid():N}"[..16];
        int rowId;

        await using (var setupDbContext = _fixture.CreateDbContext())
        {
            var row = new CiselnikTypuUkoluEntity
            {
                Kod = code,
                Nazev = "API delete row",
                IsLocked = false
            };

            setupDbContext.CiselnikTypuUkolu.Add(row);
            await setupDbContext.SaveChangesAsync();
            rowId = row.Id;
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(HttpMethod.Post, $"/Ciselniky/DeleteRow?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("Key", "typy-ukolu"),
                ("Id", rowId.ToString()))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Contain("typy-ukolu");

        await using var verificationDbContext = _fixture.CreateDbContext();
        (await verificationDbContext.CiselnikTypuUkolu.AnyAsync(x => x.Id == rowId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRow_ShouldReturnForbiddenPayload_ForLockedRow_WhenUserIsNotSuperAdmin()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorDeleteLocked");
        var rowId = await GetLockedTaskStatusRowIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/DeleteRow?asUser={editorId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "stavy-ukolu"),
                ("Id", rowId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Nemáte oprávnění");
    }

    [Fact]
    public async Task DeleteRow_ShouldRedirectWithTempData_WhenRowDoesNotExist_ForNonAjaxRequest()
    {
        // Viz komentář u SaveRow_ShouldRedirectWithTempData… — AllowAutoRedirect=false +
        // explicitní ?asUser= v follow-up GET (TestAuthHandler vyžaduje query/header).
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Ciselniky/DeleteRow?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("Key", "typy-ukolu"),
                ("Id", "99999999"))
        };

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var indexResponse = await client.GetAsync($"/Ciselniky?id=typy-ukolu&asUser={_fixture.AdminOsobaId}");
        var html = await indexResponse.Content.ReadAsStringAsync();

        indexResponse.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("<gov-message color=\"error\">");
        html.Should().Contain("nebyla nalezena");
    }

    [Fact]
    public async Task DeleteRow_ShouldReturnForbiddenPayload_ForScheduleDictionary_WhenUserIsNotSuperAdmin()
    {
        var editorId = await EnsureDictionaryEditorUserAsync("ApiDictEditorDeleteHarmonogram");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/DeleteRow?asUser={editorId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "harmonogram-kroky"),
                ("Id", "1")));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Nemáte oprávnění");
    }

    [Fact]
    public async Task DeleteRow_ShouldReturnOperationErrorPayload_WhenRowIsReferenced()
    {
        int statusId;

        await using (var setupDbContext = _fixture.CreateDbContext())
        {
            statusId = await setupDbContext.CiselnikStavuProjektu
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync();

            setupDbContext.Projekty.Add(new ProjektEntity
            {
                Zkratka = $"APIREF{Guid.NewGuid():N}"[..11],
                CelyNazev = "API referenced project status",
                StavId = statusId
            });

            await setupDbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Ciselniky/DeleteRow?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Key", "stavy-projektu"),
                ("Id", statusId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("nelze smazat");
        payload.Message.Should().Contain("používána v aplikaci");
    }

    private async Task<int> EnsureDictionaryEditorUserAsync(string marker)
    {
        var userId = await _fixture.EnsurePersonAsync(marker);

        await using var dbContext = _fixture.CreateDbContext();

        // F4 redesign 2026-04-23: ciselniky.edit rozdělen na row.edit / row.delete.
        var permissionId = await dbContext.AuthzPermissions
            .Where(x => x.IsActive && x.Klic == PermissionKeys.CiselnikyRowEdit)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        permissionId.Should().HaveValue();

        var roleId = await (
                from role in dbContext.AuthzRoles
                join rolePermission in dbContext.AuthzRolePermissions on role.Id equals rolePermission.RoleId
                where role.IsActive
                    && rolePermission.IsAllowed
                    && rolePermission.PermissionId == permissionId.Value
                    && role.Kod != "SUPERADMIN"
                orderby role.Id
                select (int?)role.Id)
            .FirstOrDefaultAsync();

        if (!roleId.HasValue)
        {
            var role = new AuthzRoleEntity
            {
                Kod = $"DICT_EDITOR_{userId}",
                Nazev = $"Dictionary Editor {userId}",
                Popis = "Generated by API tests",
                IsSystem = false,
                IsActive = true
            };

            dbContext.AuthzRoles.Add(role);
            await dbContext.SaveChangesAsync();

            dbContext.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
            {
                RoleId = role.Id,
                PermissionId = permissionId.Value,
                ScopeMode = ScopeMode.All,
                IsAllowed = true
            });

            await dbContext.SaveChangesAsync();
            roleId = role.Id;
        }

        var assignment = await dbContext.AuthzUserRoles
            .FirstOrDefaultAsync(x => x.OsobaId == userId && x.RoleId == roleId.Value);

        if (assignment is null)
        {
            dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
            {
                OsobaId = userId,
                RoleId = roleId.Value,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            assignment.IsActive = true;
        }

        var superadminRows = await dbContext.AuthzSuperadmins
            .Where(x => x.OsobaId == userId)
            .ToListAsync();

        if (superadminRows.Count > 0)
        {
            dbContext.AuthzSuperadmins.RemoveRange(superadminRows);
        }

        await dbContext.SaveChangesAsync();
        return userId;
    }

    private async Task<int> GetUnlockedTaskTypeRowIdAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var existingId = await dbContext.CiselnikTypuUkolu
            .AsNoTracking()
            .Where(x => !x.IsLocked)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var row = new CiselnikTypuUkoluEntity
        {
            Kod = $"API_UNLOCK_{Guid.NewGuid():N}"[..18],
            Nazev = "API unlocked task type",
            IsLocked = false
        };

        dbContext.CiselnikTypuUkolu.Add(row);
        await dbContext.SaveChangesAsync();
        return row.Id;
    }

    private async Task<int> GetLockedTaskStatusRowIdAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var existingId = await dbContext.CiselnikStavuUkolu
            .AsNoTracking()
            .Where(x => x.IsLocked)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var row = new CiselnikStavuUkoluEntity
        {
            Kod = $"API_LOCK_{Guid.NewGuid():N}"[..16],
            Nazev = "API locked task status",
            IsFinal = false,
            IsLocked = true
        };

        dbContext.CiselnikStavuUkolu.Add(row);
        await dbContext.SaveChangesAsync();
        return row.Id;
    }
}
