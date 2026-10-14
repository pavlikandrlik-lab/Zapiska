using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Změna stavu platná k jednání (spec 2026-10-13): co uložení změny stavu zapíše do historie,
/// co nabídne editor a jak se historie chová při přeplánování a smazání jednání.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class StatusChangeEffectiveDateDataStoreTests
{
    // 13.10.2026 00:30 v Praze = 12.10. 22:30 UTC — místní a UTC datum se liší.
    private static readonly DateTimeOffset JustAfterMidnightInPrague = new(2026, 10, 12, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateTime RecordFoundedOn = new(2026, 9, 1);

    private readonly SqlIntegrationFixture _fixture;

    public StatusChangeEffectiveDateDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    /// <summary>Review focus 2: formulář bez pole StavPlatiK — změna platí k dnešnímu místnímu datu.</summary>
    [Fact]
    public async Task SaveRecord_StatusChangeWithoutChoice_IsEffectiveOnLocalToday_AndKeepsUtcMomentOfSaving()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_today");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(JustAfterMidnightInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFTODAY");

        store.SaveRecord(await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode), arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == arranged.RecordId);
        change.DatumZmeny.Should().Be(new DateTime(2026, 10, 12, 22, 30, 0), "datum_zmeny zůstává okamžik uložení v UTC");
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 13), "„dnes“ je místní datum, ne datum v UTC");
        change.JednaniId.Should().BeNull();
    }

    private sealed record ArrangedTask(int ProjectId, int RecordId, CurrentUserContextViewModel User, string DoneStatusCode);

    private static async Task<ArrangedTask> ArrangeTaskAsync(PmTrackerDbContext dbContext, string marker)
    {
        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Admin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Owner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", marker);

        // Založení před všemi jednáními v testech, aby spodní mez (spec §4.4) nic nevyřadila.
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = RecordFoundedOn;
        await dbContext.SaveChangesAsync();

        var doneStatusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();

        return new ArrangedTask(
            projectId,
            recordId,
            IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true),
            doneStatusCode);
    }

    private static async Task<SaveRecordCommand> BuildEditCommandAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        string statusCode,
        DateTime? datumZalozeni = null)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).SingleAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).SingleAsync();

        return new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = datumZalozeni ?? record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu
        };
    }

    private sealed class PragueTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    }
}
