using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Serverové PDF (2026-09-04), spec §8: když sazba selže, tisk se vrátí na dnešní
/// HTML stránku. Bez tohoto pinu by výpadek prohlížeče na serveru shodil tisk úplně.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportPdfFallbackTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportPdfFallbackTests(ApiSqlFixture fixture) => _fixture = fixture;

    private FakePdfRenderer Renderer => _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>();

    private async Task<(int ProjectId, int MeetingId)> SeedAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"ApiPdfFb{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync($"APIPDFFB{marker}");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"APIPDFFBSUB{marker}", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(
            projectId, ownerId, subsystemId, "U", $"API pdf fallback record {marker}");
        var meetingId = await _fixture.EnsureMeetingAsync(projectId);
        return (projectId, meetingId);
    }

    [Fact]
    public async Task ProjektTisk_FallsBackToHtml_WhenRendererFails()
    {
        var (projectId, _) = await SeedAsync("PRJ");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        Renderer.ShouldFail = true;
        try
        {
            var response = await client.GetAsync(
                $"/Export/Projekt/{projectId}/Tisk?asUser={_fixture.AdminOsobaId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "výpadek sazby nesmí uživateli shodit tisk");
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("records-table", "vrací se dnešní tisková stránka");
            body.Should().Contain("window.print()",
                "na HTML cestě se má tiskový dialog stále vyvolat sám");
        }
        finally
        {
            Renderer.ShouldFail = false;
        }
    }

    [Fact]
    public async Task JednaniTisk_FallsBackToHtml_WhenRendererFails()
    {
        var (projectId, meetingId) = await SeedAsync("JED");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        Renderer.ShouldFail = true;
        try
        {
            var response = await client.GetAsync(
                $"/Export/Jednani/{meetingId}/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        }
        finally
        {
            Renderer.ShouldFail = false;
        }
    }
}
