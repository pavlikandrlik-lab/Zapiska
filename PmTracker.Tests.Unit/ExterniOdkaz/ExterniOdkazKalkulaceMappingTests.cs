using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>Snímek skutečné ceny z kalkulace žije u vazby (spec 2026-09-10 A4).</summary>
public sealed class ExterniOdkazKalkulaceMappingTests
{
    private static PmTrackerDbContext CreateDb()
        => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Theory]
    [InlineData(nameof(ZaznamExterniOdkazEntity.KalkulaceCena), "kalkulace_cena")]
    [InlineData(nameof(ZaznamExterniOdkazEntity.KalkulaceId), "kalkulace_id")]
    [InlineData(nameof(ZaznamExterniOdkazEntity.KalkulaceNacteno), "kalkulace_nacteno")]
    public void Snimek_SeMapujeNaSvujSloupec(string vlastnost, string sloupec)
    {
        using var db = CreateDb();

        var property = db.Model.FindEntityType(typeof(ZaznamExterniOdkazEntity))!.FindProperty(vlastnost);

        property.Should().NotBeNull();
        property!.GetColumnName().Should().Be(sloupec);
        property.IsNullable.Should().BeTrue("stávající vazby zůstávají prázdné, doplní je harvest");
    }
}
