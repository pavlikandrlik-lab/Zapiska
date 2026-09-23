using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Odpověď náhledu tiketu nese i popis, ze kterého se předvyplní text požadavku
/// (spec 2026-09-08 §5.4). Do 2026-09-08 vracela jen Strucne, takže klient popis neměl
/// odkud vzít.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExterniOdkazSyncPopisTests
{
    private readonly ApiSqlFixture _fixture;

    public ExterniOdkazSyncPopisTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sync_OdpovedNesePolePopis()
    {
        var projectId = await _fixture.EnsureProjectAsync("APISYNCPOPIS");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/ExterniOdkaz/Sync?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("cislo", "999999"),
                ("projektId", projectId.ToString()))));

        var json = await response.Content.ReadAsStringAsync();

        // ServiceDesk je v testech vypnutý (DisabledTicketingQueryService), takže tiket
        // nebude nalezen — ověřujeme tvar kontraktu, ne obsah. Bez pole popis by klient
        // neměl co do editoru vložit.
        json.Should().Contain("popis", "kontrakt odpovědi musí nést popis tiketu");
    }
}
