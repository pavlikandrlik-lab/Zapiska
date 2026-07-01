using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ZakladniDatasetLoaderTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ZakladniDatasetLoaderTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LoadAsync_LoadsProjectRecordsBoundedByPeriodEnd()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_loader");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DatasetOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "DATASET1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "DATASET1_SYS", personId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        // EnsureRecordAsync nastaví DatumZalozeni=Today; pro test filtru období je přepíšeme:
        // jeden záznam 2025-03 (v období), druhý 2026-01 (po konci období Obdobi.Rok(2025) → vyloučen).
        var inPeriodId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "InPeriod");
        var futureId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "FutureCreated");
        var inPeriod = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == inPeriodId);
        inPeriod.DatumZalozeni = new DateTime(2025, 3, 1);
        var future = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == futureId);
        future.DatumZalozeni = new DateTime(2026, 1, 5);
        await dbContext.SaveChangesAsync();

        var loader = new ZakladniDatasetLoader(dbContext);
        var dataset = await loader.LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.Records.Should().ContainSingle(r => r.DatumZalozeni == new DateTime(2025, 3, 1));
        dataset.Records.Should().NotContain(r => r.DatumZalozeni.Year == 2026);
        dataset.Subsystemy.Should().Contain(s => s.Id == subsystemId);
        dataset.Stavy.Should().NotBeEmpty();
        dataset.ProjektNazev.Should().NotBeNullOrWhiteSpace("hlavička reportu = název projektu");
    }

    [Fact]
    public async Task LoadAsync_FillsDatumDokonceni_FromLastFinalStateTransition()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_dokonceni");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DokonceniOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "DOKONCENI1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "DOKONCENI1_SYS", personId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "Dokonceno");
        // EnsureRecordAsync nastaví DatumZalozeni=Today (mimo období 2025) → posuneme do období.
        var seededRecord = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == recordId);
        seededRecord.DatumZalozeni = new DateTime(2025, 3, 1);
        await dbContext.SaveChangesAsync();

        var finalStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        // PuvodniStav má FK na ciselnik_stavu_ukolu → použij existující (non-final) stav.
        var nonFinalStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => !x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var dokonceniDatum = new DateTime(2025, 6, 15);
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = nonFinalStateId,
            NovyStav = finalStateId,
            DatumZmeny = dokonceniDatum
        });
        await dbContext.SaveChangesAsync();

        var loader = new ZakladniDatasetLoader(dbContext);
        var dataset = await loader.LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.Records.Should().Contain(r => r.Id == recordId && r.DatumDokonceni == dokonceniDatum);
    }
}
