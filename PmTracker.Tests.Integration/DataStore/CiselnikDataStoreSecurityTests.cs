using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class CiselnikDataStoreSecurityTests
{
    private readonly SqlIntegrationFixture _fixture;

    public CiselnikDataStoreSecurityTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveCiselnikRow_ShouldNormalizeLockState_ForNonSuperAdmin()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_lock_norm");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: false);
        var code = $"IT_LOCK_{Guid.NewGuid():N}"[..16];

        store.SaveCiselnikRow(new SaveCiselnikRowCommand
        {
            Key = "typy-ukolu",
            Kod = code,
            Nazev = "Integration lock normalization",
            IsLocked = true
        }, currentUser);

        var saved = await dbContext.CiselnikTypuUkolu
            .AsNoTracking()
            .SingleAsync(x => x.Kod == code);

        saved.IsLocked.Should().BeFalse("běžný uživatel nesmí uzamknout položku číselníku");
    }

    [Fact]
    public async Task SaveCiselnikRow_ShouldRejectLockedRowUpdate_ForNonSuperAdmin()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_locked_update");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: false);

        var lockedRow = await dbContext.CiselnikStavuUkolu
            .AsNoTracking()
            .Where(x => x.IsLocked)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .FirstAsync();

        var act = () => store.SaveCiselnikRow(new SaveCiselnikRowCommand
        {
            Key = "stavy-ukolu",
            Id = lockedRow.Id,
            Kod = lockedRow.Kod,
            Nazev = $"{lockedRow.Nazev} blocked"
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*může upravovat nebo odemykat pouze superadmin*");
    }

    [Fact]
    public async Task DeleteCiselnikRow_ShouldRejectLockedRowDelete_ForNonSuperAdmin()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_locked_delete");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: false);

        var lockedRowId = await dbContext.CiselnikStavuUkolu
            .AsNoTracking()
            .Where(x => x.IsLocked)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var act = () => store.DeleteCiselnikRow(new DeleteCiselnikRowCommand
        {
            Key = "stavy-ukolu",
            Id = lockedRowId
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*může mazat pouze superadmin*");
    }

    [Fact]
    public async Task SaveCiselnikRow_ShouldRejectSuperAdminOnlyDictionary_ForNonSuperAdmin()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_harmonogram_guard");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: false);

        var act = () => store.SaveCiselnikRow(new SaveCiselnikRowCommand
        {
            Key = "harmonogram-kroky",
            Kod = "STEP_BLOCKED",
            Nazev = "Blocked"
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Číselník harmonogramu může upravovat pouze superadmin*");
    }

    [Fact]
    public async Task DeleteCiselnikRow_ShouldRejectSuperAdminOnlyDictionary_ForNonSuperAdmin()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_harmonogram_delete_guard");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: false);

        var act = () => store.DeleteCiselnikRow(new DeleteCiselnikRowCommand
        {
            Key = "harmonogram-kroky",
            Id = 1
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Číselník harmonogramu může upravovat pouze superadmin*");
    }

    [Fact]
    public async Task SaveCiselnikRow_ShouldRejectUnknownDictionaryKey()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_unknown_save");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var act = () => store.SaveCiselnikRow(new SaveCiselnikRowCommand
        {
            Key = "unknown-dictionary",
            Kod = "X",
            Nazev = "Unknown"
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Neznámý číselník 'unknown-dictionary'*");
    }

    [Fact]
    public async Task DeleteCiselnikRow_ShouldRejectUnknownDictionaryKey()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_unknown_delete");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);
        var existingStatusId = await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var act = () => store.DeleteCiselnikRow(new DeleteCiselnikRowCommand
        {
            Key = "unknown-dictionary",
            Id = existingStatusId
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Neznámý číselník 'unknown-dictionary'*");
    }

    [Fact]
    public async Task DeleteCiselnikRow_ShouldWrapReferencedRowDeleteError()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_delete_fk_guard");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var statusRows = await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync();
        var statusId = statusRows
            .Where(x =>
                string.Equals(x.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Nazev) && x.Nazev.Contains("smaz", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .First();

        Exception? captured = null;
        try
        {
            store.DeleteCiselnikRow(new DeleteCiselnikRowCommand
            {
                Key = "stavy-projektu",
                Id = statusId
            }, currentUser);
        }
        catch (Exception ex)
        {
            captured = ex;
        }

        if (captured is null)
        {
            var exists = await dbContext.CiselnikStavuProjektu.AsNoTracking().AnyAsync(x => x.Id == statusId);
            exists.Should().BeFalse("pokud provider dovolí kaskádové mazání, položka musí být skutečně odstraněna");
            return;
        }

        captured.Should().BeOfType<InvalidOperationException>();
        captured.Message.Should().Contain("nelze smazat, protože je používána v aplikaci");
    }

    [Fact]
    public async Task SaveCiselnikRow_ShouldWriteAuditLogEntry()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_save_audit");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);
        var code = $"IT_AUDIT_{Guid.NewGuid():N}"[..17];
        var auditBefore = await dbContext.AuthzAuditLog
            .AsNoTracking()
            .Select(x => (long?)x.Id)
            .MaxAsync() ?? 0;

        store.SaveCiselnikRow(new SaveCiselnikRowCommand
        {
            Key = "typy-ukolu",
            Kod = code,
            Nazev = "Audit save row",
            IsLocked = false
        }, currentUser);

        var audit = await dbContext.AuthzAuditLog
            .AsNoTracking()
            .Where(x => x.Id > auditBefore)
            .Where(x => x.EntityType == "ciselnik")
            .Where(x => x.Action == "create")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        audit.Should().NotBeNull();
        audit!.ActorOsobaId.Should().Be(currentUser.OsobaId);
        int.Parse(audit.EntityId).Should().BeGreaterThan(0);
        audit.NewValue.Should().Contain(code);
    }

    [Fact]
    public async Task DeleteCiselnikRow_ShouldWriteAuditLogEntry()
    {
        var db = await _fixture.CreateDatabaseAsync("dict_delete_audit");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);
        var row = new CiselnikTypuUkoluEntity
        {
            Kod = $"IT_DEL_AUD_{Guid.NewGuid():N}"[..18],
            Nazev = "Audit delete row",
            IsLocked = false
        };

        dbContext.CiselnikTypuUkolu.Add(row);
        await dbContext.SaveChangesAsync();

        var auditBefore = await dbContext.AuthzAuditLog
            .AsNoTracking()
            .Select(x => (long?)x.Id)
            .MaxAsync() ?? 0;

        store.DeleteCiselnikRow(new DeleteCiselnikRowCommand
        {
            Key = "typy-ukolu",
            Id = row.Id
        }, currentUser);

        var audit = await dbContext.AuthzAuditLog
            .AsNoTracking()
            .Where(x => x.Id > auditBefore)
            .Where(x => x.EntityType == "ciselnik")
            .Where(x => x.Action == "delete")
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        audit.Should().NotBeNull();
        audit!.ActorOsobaId.Should().Be(currentUser.OsobaId);
        audit.EntityId.Should().Be(row.Id.ToString());
        audit.OldValue.Should().Contain("Audit delete row");
        audit.NewValue.Should().BeNull();
    }
}
