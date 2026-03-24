using System.Net;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Web.Tests.Integration;

public sealed class HttpSmokeTests(ApiSqlFixture fixture) : IClassFixture<ApiSqlFixture>
{
    [Fact]
    public async Task GetProjekty_ShouldReturnOk()
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty?asUser={fixture.AdminOsobaId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJednani_ShouldReturnOk()
    {
        using var client = fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Jednani?asUser={fixture.AdminOsobaId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
