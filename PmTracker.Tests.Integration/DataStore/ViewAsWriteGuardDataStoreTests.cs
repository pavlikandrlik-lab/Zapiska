using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>Pojistka pohledu jako proti reálnému SQL Serveru (InMemory DbCommand nepouští).</summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ViewAsWriteGuardDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ViewAsWriteGuardDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static PmTrackerDbContext Create(string connectionString, ViewAsRequestState state)
        => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new ViewAsWriteGuardInterceptor(state, NullLogger<ViewAsWriteGuardInterceptor>.Instance))
            .Options);

    private static ViewAsRequestState Active()
    {
        var state = new ViewAsRequestState();
        state.Activate(1, 2);
        return state;
    }

    [Fact]
    public async Task ActiveViewAs_ReadsWork()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_reads");
        await using var dbContext = Create(db.ConnectionString, Active());

        var count = await dbContext.Osoby.CountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ActiveViewAs_SaveChangesInsert_IsBlocked()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_insert");
        await using var dbContext = Create(db.ConnectionString, Active());
        var orgId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync();
        dbContext.Osoby.Add(new OsobaEntity { Jmeno = "Blok", Prijmeni = "Zapisu", OrganizaceId = orgId });

        var act = () => dbContext.SaveChangesAsync();

        (await act.Should().ThrowAsync<Exception>())
            .Which.Should().Match<Exception>(ex => ViewAsWriteBlockedException.IsInChain(ex));
        await using var check = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        (await check.Osoby.AnyAsync(x => x.Prijmeni == "Zapisu")).Should().BeFalse();
    }

    [Fact]
    public async Task ActiveViewAs_ExecuteUpdate_IsBlocked()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_execupdate");
        await using var dbContext = Create(db.ConnectionString, Active());

        var act = () => dbContext.Osoby.Where(x => x.Id < 0).ExecuteUpdateAsync(s => s.SetProperty(x => x.Jmeno, "X"));

        await act.Should().ThrowAsync<ViewAsWriteBlockedException>();
    }

    [Fact]
    public async Task ActiveViewAs_RawMerge_IsBlocked()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_merge");
        await using var dbContext = Create(db.ConnectionString, Active());

        var act = () => dbContext.Database.ExecuteSqlRawAsync(
            "MERGE dbo.zaznam_edit_zamek AS t USING (SELECT -1 AS zaznam_id) AS s ON t.zaznam_id = s.zaznam_id WHEN MATCHED THEN DELETE;");

        await act.Should().ThrowAsync<ViewAsWriteBlockedException>();
    }

    [Fact]
    public async Task InactiveOrPermitted_WritesWork()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_permitted");
        var permitted = Active();
        permitted.PermitWrites();

        foreach (var state in new[] { new ViewAsRequestState(), permitted })
        {
            await using var dbContext = Create(db.ConnectionString, state);
            var orgId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync();
            dbContext.Osoby.Add(new OsobaEntity { Jmeno = "Smi", Prijmeni = "Zapsat", OrganizaceId = orgId });

            await dbContext.SaveChangesAsync();
        }

        await using var check = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        (await check.Osoby.CountAsync(x => x.Prijmeni == "Zapsat")).Should().Be(2);
    }
}
