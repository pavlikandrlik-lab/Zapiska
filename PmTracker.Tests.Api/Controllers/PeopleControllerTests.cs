using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class PeopleControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public PeopleControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AdPersonModal_ShouldRenderModal_ForAuthorizedUser()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Osoby/AdPersonModal?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("ad-person-modal-title");
        html.Should().Contain("data-search-url=\"/Osoby/SearchAd\"");
    }

    [Fact]
    public async Task Index_ShouldRenderSearch_AndSortablePeopleTable()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Osoby?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-osoby-table-card");
        html.Should().Contain("data-table-tools-root");
        html.Should().Contain("data-table-tools-search-input");
        html.Should().Contain("data-table-tools-table");
        html.Should().Contain("data-table-sort-button");
        html.Should().Contain("Žádná osoba neodpovídá zadanému filtru.");
    }

    [Fact]
    public async Task AdPersonModal_ShouldReturnForbidden_WhenUserContextCannotBeResolved()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Osoby/AdPersonModal?asUser=missing-user");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ManualPersonModal_ShouldRenderCreateModal_ForAuthorizedUser()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Osoby/ManualPersonModal?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("manual-person-modal-title");
        html.Should().Contain("name=\"Jmeno\"");
        html.Should().Contain("name=\"Prijmeni\"");
    }

    [Fact]
    public async Task ManualPersonModal_ShouldRenderAdEditMode_ForAdAccount()
    {
        var adPerson = await CreateAdPersonAsync(
            marker: "ApiPeopleManualAdModal",
            guidAd: Guid.NewGuid(),
            adLogin: "acr\\api.people.manual.modal",
            locationLocked: true);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Osoby/ManualPersonModal?id={adPerson.Id}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain($"name=\"Id\" value=\"{adPerson.Id}\"");
        html.Should().Contain($"name=\"Jmeno\" value=\"{adPerson.Jmeno}\" required readonly");
        html.Should().Contain($"name=\"Prijmeni\" value=\"{adPerson.Prijmeni}\" required readonly");
        html.Should().Contain($"name=\"Email\" value=\"{adPerson.Email}\"");
        html.Should().Contain("name=\"LocationLocked\" value=\"true\" checked");
    }

    [Fact]
    public async Task ManualPersonModal_ShouldReturnNotFound_WhenPersonIsMissing()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Osoby/ManualPersonModal?id={int.MaxValue}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ManualPersonModal_ShouldReturnForbidden_WhenUserContextCannotBeResolved()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Osoby/ManualPersonModal?asUser=missing-user");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SearchAd_ShouldReturnEmptyPayload_ForBlankQuery()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Osoby/SearchAd?q=&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("available").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("results").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SearchAd_ShouldReturnForbidden_WhenUserContextCannotBeResolved()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxGet("/Osoby/SearchAd?q=test&asUser=missing-user");
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SaveManual_ShouldCreatePerson_AndReturnAjaxSuccess()
    {
        var selection = await GetLocationSelectionAsync();
        var email = "api.people.manual.success@pmtracker.test";

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveManual",
            ("Jmeno", "Manual"),
            ("Prijmeni", "Success"),
            ("Titul", "Bc."),
            ("Email", email),
            ("Organizace", selection.Organization),
            ("OrganizacniCelek", selection.OrgUnit ?? string.Empty),
            ("LocationLocked", "false"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.Message.Should().Be("Osoba byla uložena.");
        payload.RefreshScope.Should().Be("osoby-index");
        payload.RefreshUrl.Should().Be("/Osoby");

        await using var dbContext = _fixture.CreateDbContext();
        var person = await dbContext.Osoby
            .AsNoTracking()
            .SingleAsync(x => x.Email == email);
        person.Jmeno.Should().Be("Manual");
        person.Prijmeni.Should().Be("Success");
        person.GuidAd.Should().BeNull();
        person.LocationLocked.Should().BeFalse();
    }

    [Fact]
    public async Task SaveManual_ShouldReturnJsonForbiddenPayload_WhenUserLacksPermission()
    {
        var selection = await GetLocationSelectionAsync();
        var userId = await _fixture.EnsurePersonAsync("ApiPeopleSaveManualDenied");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            userId,
            "SaveManual",
            ("Jmeno", "Manual"),
            ("Prijmeni", "Denied"),
            ("Email", "api.people.manual.denied@pmtracker.test"),
            ("Organizace", selection.Organization),
            ("OrganizacniCelek", selection.OrgUnit ?? string.Empty));

        var response = await client.SendAsync(request);

        await AssertForbiddenAjaxPayloadAsync(response);
    }

    [Fact]
    public async Task SaveManual_ShouldReturnValidationError_WhenRequiredFieldIsMissing()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveManual",
            ("Prijmeni", "Validation"),
            ("Email", "api.people.manual.validation@pmtracker.test"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("REQUEST_VALIDATION_FAILED");
        payload.Message.Should().Be("Osobu nelze uložit.");
        payload.FieldErrors.Keys.Should().Contain("Jmeno");
    }

    [Fact]
    public async Task SaveManual_ShouldReturnAjaxError_WhenEditedPersonDoesNotExist()
    {
        var selection = await GetLocationSelectionAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveManual",
            ("Id", int.MaxValue.ToString()),
            ("Jmeno", "Missing"),
            ("Prijmeni", "Person"),
            ("Email", "api.people.manual.missing@pmtracker.test"),
            ("Organizace", selection.Organization),
            ("OrganizacniCelek", selection.OrgUnit ?? string.Empty));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("nebyla nalezena");
    }

    [Fact]
    public async Task SaveManual_ShouldOnlyUpdateLocation_WhenEditingAdPerson()
    {
        var adPerson = await CreateAdPersonAsync(
            marker: "ApiPeopleManualAdUpdate",
            guidAd: Guid.NewGuid(),
            adLogin: "acr\\api.people.manual.update",
            locationLocked: false);
        var location = await GetLocationSelectionAsync(adPerson.OrganizationId, adPerson.OrgUnitId);
        var originalOrgId = adPerson.OrganizationId;
        var originalOrgUnitId = adPerson.OrgUnitId;

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveManual",
            ("Id", adPerson.Id.ToString()),
            ("Jmeno", "ChangedNameShouldBeIgnored"),
            ("Prijmeni", "ChangedSurnameShouldBeIgnored"),
            ("Email", "changed.email@pmtracker.test"),
            ("Organizace", location.Organization),
            ("OrganizacniCelek", location.OrgUnit ?? string.Empty),
            ("LocationLocked", "true"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();

        await using var dbContext = _fixture.CreateDbContext();
        var saved = await dbContext.Osoby
            .AsNoTracking()
            .SingleAsync(x => x.Id == adPerson.Id);

        saved.Jmeno.Should().Be(adPerson.Jmeno);
        saved.Prijmeni.Should().Be(adPerson.Prijmeni);
        saved.Email.Should().Be(adPerson.Email);
        saved.LocationLocked.Should().BeTrue();
        saved.OrganizaceId.Should().Be(location.OrganizationId);
        if (location.OrganizationId != originalOrgId)
        {
            saved.OrganizaceId.Should().NotBe(originalOrgId);
        }

        saved.OrganizacniCelekId.Should().Be(location.OrgUnitId);
        if (location.OrgUnitId.HasValue && location.OrgUnitId != originalOrgUnitId)
        {
            saved.OrganizacniCelekId.Should().NotBe(originalOrgUnitId);
        }
    }

    [Fact]
    public async Task SaveAd_ShouldCreatePerson_AndReturnAjaxSuccess()
    {
        var selection = await GetLocationSelectionAsync();
        var guid = Guid.NewGuid();
        var email = "api.people.ad.success@pmtracker.test";

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveAd",
            ("VyhledavaciRetezec", "ad success"),
            ("Jmeno", "Ad"),
            ("Prijmeni", "Success"),
            ("Titul", "Mgr."),
            ("GuidAd", guid.ToString()),
            ("AdLogin", "acr\\ad.success"),
            ("AdCompany", selection.Organization),
            ("AdDepartment", selection.OrgUnit ?? string.Empty),
            ("Organizace", selection.Organization),
            ("OrganizacniCelek", selection.OrgUnit ?? string.Empty),
            ("Email", email),
            ("LocationLocked", "true"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.Message.Should().Be("AD osoba byla uložena.");
        payload.RefreshScope.Should().Be("osoby-index");
        payload.RefreshUrl.Should().Be("/Osoby");

        await using var dbContext = _fixture.CreateDbContext();
        var person = await dbContext.Osoby
            .AsNoTracking()
            .SingleAsync(x => x.GuidAd == guid);
        person.Email.Should().Be(email);
        person.AdLogin.Should().Be("acr\\ad.success");
        person.LocationLocked.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAd_ShouldUpdateExistingPerson_WhenGuidAlreadyExists()
    {
        var location = await GetLocationSelectionAsync();
        var guid = Guid.NewGuid();
        var existing = await CreateAdPersonAsync(
            marker: "ApiPeopleAdUpsert",
            guidAd: guid,
            adLogin: "acr\\api.people.ad.existing",
            locationLocked: false);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveAd",
            ("VyhledavaciRetezec", "ad upsert"),
            ("Jmeno", "UpdatedAdName"),
            ("Prijmeni", "UpdatedAdSurname"),
            ("Titul", "Ing."),
            ("GuidAd", guid.ToString()),
            ("AdLogin", "ACR\\API.PEOPLE.AD.UPDATED"),
            ("AdCompany", location.Organization),
            ("AdDepartment", location.OrgUnit ?? string.Empty),
            ("Organizace", location.Organization),
            ("OrganizacniCelek", location.OrgUnit ?? string.Empty),
            ("Email", "api.people.ad.upsert.updated@pmtracker.test"),
            ("LocationLocked", "true"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.Message.Should().Be("AD osoba byla uložena.");

        await using var dbContext = _fixture.CreateDbContext();
        var peopleWithGuid = await dbContext.Osoby
            .AsNoTracking()
            .Where(x => x.GuidAd == guid)
            .ToListAsync();
        peopleWithGuid.Should().HaveCount(1);

        var saved = peopleWithGuid[0];
        saved.Id.Should().Be(existing.Id);
        saved.Jmeno.Should().Be("UpdatedAdName");
        saved.Prijmeni.Should().Be("UpdatedAdSurname");
        saved.Email.Should().Be("api.people.ad.upsert.updated@pmtracker.test");
        saved.AdLogin.Should().Be("acr\\api.people.ad.updated");
        saved.LocationLocked.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAd_ShouldReturnJsonForbiddenPayload_WhenUserLacksPermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiPeopleSaveAdDenied");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            userId,
            "SaveAd",
            ("VyhledavaciRetezec", "denied"),
            ("Jmeno", "Ad"),
            ("Prijmeni", "Denied"),
            ("GuidAd", Guid.NewGuid().ToString()),
            ("Email", "api.people.ad.denied@pmtracker.test"));

        var response = await client.SendAsync(request);

        await AssertForbiddenAjaxPayloadAsync(response);
    }

    [Fact]
    public async Task SaveAd_ShouldReturnValidationError_WhenGuidIsMissing()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "SaveAd",
            ("VyhledavaciRetezec", "validation"),
            ("Jmeno", "Ad"),
            ("Prijmeni", "Validation"),
            ("Email", "api.people.ad.validation@pmtracker.test"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("REQUEST_VALIDATION_FAILED");
        payload.Message.Should().Be("AD osobu nelze uložit.");
        payload.FieldErrors.Keys.Should().Contain("GuidAd");
    }

    [Fact]
    public async Task Delete_ShouldDeletePerson_AndReturnAjaxSuccess()
    {
        var personId = await _fixture.EnsurePersonAsync("ApiPeopleDeleteSuccess");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "Delete",
            ("Id", personId.ToString()));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.Message.Should().Be("Osoba byla odstraněna.");
        payload.RefreshScope.Should().Be("osoby-index");
        payload.RefreshUrl.Should().Be("/Osoby");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Osoby.AnyAsync(x => x.Id == personId)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldReturnJsonForbiddenPayload_WhenUserLacksPermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiPeopleDeleteDenied");
        var personId = await _fixture.EnsurePersonAsync("ApiPeopleDeleteDeniedTarget");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            userId,
            "Delete",
            ("Id", personId.ToString()));

        var response = await client.SendAsync(request);

        await AssertForbiddenAjaxPayloadAsync(response);

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Osoby.AnyAsync(x => x.Id == personId)).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_ShouldReturnValidationError_WhenIdIsNotNumeric()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "Delete",
            ("Id", "not-a-number"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("REQUEST_VALIDATION_FAILED");
        payload.Message.Should().Be("Osobu nelze odstranit.");
        payload.FieldErrors.Keys.Should().Contain("Id");
    }

    [Fact]
    public async Task Delete_ShouldReturnAjaxError_WhenPersonDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildAjaxPost(
            _fixture.AdminOsobaId,
            "Delete",
            ("Id", int.MaxValue.ToString()));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Be("Osoba nebyla nalezena.");
    }

    [Fact]
    public async Task SaveManual_ShouldRedirectToIndex_WhenRequestIsNotAjaxAndSucceeds()
    {
        var location = await GetLocationSelectionAsync();
        var email = "api.people.manual.redirect.success@pmtracker.test";

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildNonAjaxPost(
            _fixture.AdminOsobaId,
            "SaveManual",
            ("Jmeno", "ManualRedirect"),
            ("Prijmeni", "Success"),
            ("Email", email),
            ("Organizace", location.Organization),
            ("OrganizacniCelek", location.OrgUnit ?? string.Empty),
            ("LocationLocked", "false"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/Osoby");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Osoby.AnyAsync(x => x.Email == email)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAd_ShouldRedirectAndShowTempDataError_WhenRequestIsNotAjaxAndModelIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildNonAjaxPost(
            _fixture.AdminOsobaId,
            "SaveAd",
            ("VyhledavaciRetezec", "invalid"),
            ("Jmeno", "Ad"),
            ("Prijmeni", "Invalid"),
            ("Email", "api.people.ad.nonajax.invalid@pmtracker.test"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/Osoby");

        var indexResponse = await client.GetAsync($"/Osoby?asUser={_fixture.AdminOsobaId}");
        var html = await indexResponse.Content.ReadAsStringAsync();
        // _Layout.cshtml renderuje TempData["ErrorMessage"] přes <gov-message color="error">
        // (původně `alert alert-error`). Fáze 2 gov-design-system migrace.
        html.Should().Contain("<gov-message color=\"error\">");
        html.Should().Contain("AD osobu nelze ulo");
    }

    [Fact]
    public async Task Delete_ShouldRedirectAndShowTempDataError_WhenRequestIsNotAjaxAndModelIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = BuildNonAjaxPost(
            _fixture.AdminOsobaId,
            "Delete",
            ("Id", "not-a-number"));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().Be("/Osoby");

        var indexResponse = await client.GetAsync($"/Osoby?asUser={_fixture.AdminOsobaId}");
        var html = await indexResponse.Content.ReadAsStringAsync();
        html.Should().Contain("<gov-message color=\"error\">");
        html.Should().Contain("Osobu nelze odstranit.");
    }

    private static HttpRequestMessage BuildAjaxPost(int asUserId, string action, params (string Key, string Value)[] fields)
    {
        return ApiTestHttpHelper.BuildAjaxPost(
            $"/Osoby/{action}?asUser={asUserId}",
            ApiTestHttpHelper.BuildForm(fields));
    }

    private static HttpRequestMessage BuildNonAjaxPost(int asUserId, string action, params (string Key, string Value)[] fields)
    {
        return new HttpRequestMessage(HttpMethod.Post, $"/Osoby/{action}?asUser={asUserId}")
        {
            Content = ApiTestHttpHelper.BuildForm(fields)
        };
    }

    private async Task<(int OrganizationId, string Organization, int? OrgUnitId, string? OrgUnit)> GetLocationSelectionAsync(
        int? excludeOrganizationId = null,
        int? excludeOrgUnitId = null)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var organizations = await dbContext.CiselnikOrganizace
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync();
        var organization = organizations
            .FirstOrDefault(x => !excludeOrganizationId.HasValue || x.Id != excludeOrganizationId.Value)
            ?? organizations.First();

        var orgUnits = await dbContext.CiselnikOrganizacniCelky
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync();
        var orgUnit = orgUnits
            .FirstOrDefault(x => !excludeOrgUnitId.HasValue || x.Id != excludeOrgUnitId.Value)
            ?? orgUnits.FirstOrDefault();

        return (
            organization.Id,
            string.IsNullOrWhiteSpace(organization.Kod) ? organization.Nazev : organization.Kod,
            orgUnit?.Id,
            orgUnit is null
                ? null
                : (string.IsNullOrWhiteSpace(orgUnit.Kod) ? orgUnit.Nazev : orgUnit.Kod));
    }

    private async Task<(int Id, string Jmeno, string Prijmeni, string Email, int OrganizationId, int? OrgUnitId)> CreateAdPersonAsync(
        string marker,
        Guid guidAd,
        string adLogin,
        bool locationLocked)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var organizationId = await dbContext.CiselnikOrganizace
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var orgUnitId = await dbContext.CiselnikOrganizacniCelky
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        var person = new OsobaEntity
        {
            Jmeno = marker,
            Prijmeni = "Api",
            Titul = "Ing.",
            Email = $"{marker.ToLowerInvariant()}@pmtracker.test",
            OrganizaceId = organizationId,
            OrganizacniCelekId = orgUnitId,
            GuidAd = guidAd,
            AdLogin = adLogin.ToLowerInvariant(),
            LocationLocked = locationLocked
        };

        dbContext.Osoby.Add(person);
        await dbContext.SaveChangesAsync();

        return (person.Id, person.Jmeno, person.Prijmeni, person.Email ?? string.Empty, person.OrganizaceId, person.OrganizacniCelekId);
    }

    private static Task AssertForbiddenAjaxPayloadAsync(HttpResponseMessage response)
    {
        // Po přechodu na [Authorize(Policy = "permission:people.manage")] se autorizace řeší
        // framework-level a vrací plain 403 bez JSON payloadu. Dřívější AjaxForbiddenResult
        // s JSON tělem přicházel jen když controller dělal in-controller `hasPermission` check
        // (ExecuteCommand). Ten byl nahrazen policy attribute, takže test nyní kontroluje jen
        // status code.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        return Task.CompletedTask;
    }
}
