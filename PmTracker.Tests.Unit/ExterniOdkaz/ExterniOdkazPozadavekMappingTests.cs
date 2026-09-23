using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// Text požadavku patří k vazbě, ne k záznamu — jeden záznam může mít víc PNF
/// a každé svůj požadavek (spec 2026-09-08 §5.1).
/// </summary>
public sealed class ExterniOdkazPozadavekMappingTests
{
    private static PmTrackerDbContext CreateDb()
        => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Pozadavek_SeMapujeNaSloupecPozadavek()
    {
        using var db = CreateDb();

        var property = db.Model
            .FindEntityType(typeof(ZaznamExterniOdkazEntity))!
            .FindProperty(nameof(ZaznamExterniOdkazEntity.Pozadavek));

        property.Should().NotBeNull("bez vlastnosti se text nemá kam uložit");
        property!.GetColumnName().Should().Be("pozadavek");
        property.IsNullable.Should().BeTrue(
            "stávající vazby zůstávají prázdné, žádná migrace dat se nedělá");
    }
}
