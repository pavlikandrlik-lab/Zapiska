using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Regression guard for ZaznamNavrhEntity optimistic-concurrency configuration.
/// SQLite in-memory does not natively support row-version; we verify the EF metadata
/// instead of simulating a real concurrent write (which requires a real DB).
/// </summary>
public sealed class ZaznamNavrhEntityConcurrencyConfigTests
{
    private static IModel BuildModel()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new PmTrackerDbContext(opts);
        return db.Model;
    }

    [Fact]
    public void ZaznamNavrhEntity_RowVersion_MustBe_ConcurrencyToken()
    {
        // Arrange
        var model = BuildModel();

        // Act
        var entityType = model.FindEntityType(typeof(ZaznamNavrhEntity));
        var rowVersionProp = entityType?.FindProperty(nameof(ZaznamNavrhEntity.RowVersion));

        // Assert
        rowVersionProp.Should().NotBeNull("RowVersion property must exist on ZaznamNavrhEntity");
        rowVersionProp!.IsConcurrencyToken.Should().BeTrue(
            "RowVersion must be mapped with IsRowVersion() to enable optimistic concurrency " +
            "— without it two admins can approve the same proposal simultaneously");
    }

    [Fact]
    public void ZaznamNavrhEntity_RowVersion_MustBe_ValueGenerated_OnAddOrUpdate()
    {
        // Arrange
        var model = BuildModel();

        // Act
        var entityType = model.FindEntityType(typeof(ZaznamNavrhEntity));
        var rowVersionProp = entityType?.FindProperty(nameof(ZaznamNavrhEntity.RowVersion));

        // Assert
        rowVersionProp.Should().NotBeNull();
        rowVersionProp!.ValueGenerated.Should().Be(
            Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate,
            "a row-version column must be DB-generated on every insert and update");
    }
}
