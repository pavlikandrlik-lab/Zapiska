using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Authorization;

/// <summary>
/// HIGH-1 regression guard: dokazuje, že pipeline ordering v
/// <c>PmTracker.Web.Program</c> má <c>UserContextMiddleware</c> vloženou
/// mezi <c>UseAuthentication()</c> a <c>UseAuthorization()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Před fix: <c>BaseController.OnActionExecutionAsync</c> (action filter) volal
/// <c>UserContextResolver</c> až po tom, co <c>UseAuthorization</c> už vyhodnotil
/// <c>[Authorize(Policy = "permission:...")]</c> policies. V té fázi
/// <c>ICurrentUserAccessor.OsobaId</c> vracel <c>null</c>, takže
/// <c>PermissionAuthorizationHandler</c> nikdy nevolal <c>context.Succeed()</c> a
/// framework vrátil 401/403 pro jakoukoli policy-protected akci — i pro
/// Super Admina, který měl všechna oprávnění.
/// </para>
/// <para>
/// Po fix: middleware naplní <c>HttpContext.Items[CurrentUserAccessor.HttpContextItemKey]</c>
/// dříve, než <c>UseAuthorization</c> volá handler, takže policy evaluace vidí
/// rozřešeného uživatele a kontrolu schválí.
/// </para>
/// <para>
/// Canary endpoint je <c>GET /Nastaveni/Panel?section=role-akce</c> — celá
/// <c>NastaveniController</c> třída je dekorována
/// <c>[Authorize(Policy = "permission:settings.view")]</c>, což je ta policy, kterou
/// HIGH-1 přímo rozbíjel. Tento test checkuje POUZE status code (ne obsah HTML),
/// aby nebyl křehký vůči UI refaktoringům.
/// </para>
/// </remarks>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class PolicyPipelineIntegrationTests
{
    private readonly ApiSqlFixture _fixture;

    public PolicyPipelineIntegrationTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PolicyProtectedAction_ShouldNotReturn401_ForUserWithPermission()
    {
        // Arrange: dev-seed admin má `permission:settings.view` (Super Admin → všechny akce).
        // Route je dekorovaná `[Authorize(Policy = "permission:settings.view")]` na třídě
        // NastaveniController.
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        var response = await client.GetAsync(
            $"/Nastaveni/Panel?section=role-akce&asUser={_fixture.AdminOsobaId}");

        // Assert: HIGH-1 by zde vrátil 401 (policy handler neviděl osobaId).
        // Fix: middleware naplní osobaId před UseAuthorization → handler zavolá Succeed → 200.
        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Unauthorized, because: "HIGH-1 by zde vracel 401 — policy handler před fix neviděl osobaId");
        response.StatusCode
            .Should()
            .NotBe(HttpStatusCode.Forbidden, because: "admin má permission:settings.view, policy musí projít");
        response.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PolicyProtectedAction_ShouldReturn401_ForAnonymousRequest()
    {
        // Kontrolní test (symetrie): bez `asUser=` middleware u anonymního requestu
        // NEREsolvuje osobu, policy handler dostane osobaId=null a 401 je správně.
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        var response = await client.GetAsync("/Nastaveni/Panel?section=role-akce");

        // Assert: policy gating stále funguje pro anonymní requesty.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
