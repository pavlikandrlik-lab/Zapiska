using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Podbarvení ukončených (2026-09-05): příznak se propíše do vykreslené tiskové značky.
/// Tisk vrací PDF, proto se značka přebírá ze vstupu generátoru.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportCompletedHighlightTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportCompletedHighlightTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjektTisk_MarksCompletedRecordRow()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiDoneOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIDONE");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIDONESUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(
            projectId, ownerId, subsystemId, "U", "API completed record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var doneStateId = await dbContext.CiselnikStavuUkolu
                .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
            record.StavUkoluId = doneStateId;
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        await client.GetAsync($"/Export/Projekt/{projectId}/Tisk?asUser={_fixture.AdminOsobaId}");

        var html = _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>().LastHtml;
        html.Should().NotBeNullOrWhiteSpace();
        html.Should().Contain("task-row completed",
            "ukončený úkol musí mít v tisku třídu pro podbarvení");
    }
}
