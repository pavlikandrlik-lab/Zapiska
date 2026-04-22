using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Unit.Data;

/// <summary>
/// H-3: AuthorizationSnapshotBuilder joinuje na CiselnikRoliProjektu.AuthzRoleId
/// a CiselnikRoliSubsystemu.AuthzRoleId při každém build() snapshotu. Bez indexů
/// na těchto sloupcích je každý build table-scan přes lookup tabulky.
///
/// Test ověří, že EF model deklaruje HasIndex na AuthzRoleId pro obě entity.
/// Samotný index v databázi je vytvořen SQL skriptem db_upgrade_1_3_2_authz_join_indexes.sql
/// (offline intranet deployment — EF migrations jsou záměrně prázdné).
/// </summary>
public sealed class AuthzJoinIndexTests
{
    [Fact]
    public void CiselnikRoliProjektu_Should_HaveIndex_On_AuthzRoleId()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CiselnikRoliProjektuEntity));

        entity.Should().NotBeNull("CiselnikRoliProjektuEntity musí být v modelu");

        var hasIndex = entity!.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0].Name == nameof(CiselnikRoliProjektuEntity.AuthzRoleId));

        hasIndex.Should().BeTrue(
            "CiselnikRoliProjektu.AuthzRoleId musí mít index — join v AuthorizationSnapshotBuilder (H-3)");
    }

    [Fact]
    public void CiselnikRoliSubsystemu_Should_HaveIndex_On_AuthzRoleId()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(CiselnikRoleSubsystemuEntity));

        entity.Should().NotBeNull("CiselnikRoleSubsystemuEntity musí být v modelu");

        var hasIndex = entity!.GetIndexes()
            .Any(i => i.Properties.Count == 1
                      && i.Properties[0].Name == nameof(CiselnikRoleSubsystemuEntity.AuthzRoleId));

        hasIndex.Should().BeTrue(
            "CiselnikRoliSubsystemu.AuthzRoleId musí mít index — join v AuthorizationSnapshotBuilder (H-3)");
    }

    private static PmTrackerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase($"authz-join-index-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new PmTrackerDbContext(options);
    }
}
