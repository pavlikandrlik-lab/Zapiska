using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Serverové PDF (2026-09-04): tiskové akce vrací dokument, ne HTML stránku.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportPdfPrintTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportPdfPrintTests(ApiSqlFixture fixture) => _fixture = fixture;

    private FakePdfRenderer Renderer => _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>();

    private async Task<(int ProjectId, int RecordId)> SeedProjectWithRecordAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"ApiPdf{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync($"APIPDF{marker}");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"APIPDFSUB{marker}", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(
            projectId, ownerId, subsystemId, "U", $"API pdf print record {marker}");
        return (projectId, recordId);
    }

    [Fact]
    public async Task ProjektTisk_ReturnsPdf_OpenedInline()
    {
        var (projectId, _) = await SeedProjectWithRecordAsync("INL");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            $"/Export/Projekt/{projectId}/Tisk?asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("inline",
            "rozhodnutí U4 — PDF se otevře v prohlížečce, nestahuje se");
        disposition.FileNameStar.Should().EndWith(".pdf");
        disposition.FileNameStar.Should().Contain("projekt");
    }

    [Fact]
    public async Task ProjektTisk_FeedsRenderedTemplate_AndPrintStylesheet()
    {
        var (projectId, _) = await SeedProjectWithRecordAsync("TPL");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        await client.GetAsync($"/Export/Projekt/{projectId}/Tisk?asUser={_fixture.AdminOsobaId}");

        Renderer.LastHtml.Should().NotBeNullOrWhiteSpace();
        Renderer.LastHtml.Should().Contain("records-table",
            "do generátoru musí jít vyrenderovaná tisková šablona, ne prázdný dokument");
        Renderer.LastStylesheetPath.Should().EndWith("pdf-export.css");
    }

    [Fact]
    public async Task UkolTisk_ReturnsPdf_WithRecordNumberInFileName()
    {
        var (projectId, recordId) = await SeedProjectWithRecordAsync("UKO");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync(
            $"/Export/Ukol/{recordId}/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Contain("zaznam");
    }
}
