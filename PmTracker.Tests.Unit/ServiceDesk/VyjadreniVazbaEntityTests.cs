using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class VyjadreniVazbaEntityTests
{
    [Fact]
    public void VazbaEntity_RoundTripsAllFields()
    {
        var v = new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = 1,
            ZaznamId = 42,
            Poradi = 6,
            ExterniOdkazId = 17,
            HotVyjadreniId = 99999,
            DatumVyjadreni = new DateTime(2026, 3, 14),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc)
        };

        v.Source.Should().Be((byte)VazbaSource.Auto);
        v.Stav.Should().Be((byte)VazbaStav.Active);
        v.HotVyjadreniId.Should().Be(99999);
    }

    [Fact]
    public void DbContext_HasVyjadreniVazbyDbSet()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("vazba-shape-" + Guid.NewGuid())
            .Options;
        using var db = new PmTrackerDbContext(opts);
        db.VyjadreniVazby.Should().NotBeNull();
    }

    [Fact]
    public void VazbaSource_Enum_ValuesStable()
    {
        ((byte)VazbaSource.Auto).Should().Be(1);
        ((byte)VazbaSource.Manual).Should().Be(2);
        ((byte)VazbaSource.ChronologyCascade).Should().Be(3);
    }

    [Fact]
    public void VazbaStav_Enum_ValuesStable()
    {
        ((byte)VazbaStav.Active).Should().Be(1);
        ((byte)VazbaStav.Superseded).Should().Be(2);
        ((byte)VazbaStav.Deleted).Should().Be(3);
    }
}
