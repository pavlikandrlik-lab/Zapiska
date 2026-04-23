using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class TicketingReadOnlyDbContextShapeTests
{
    [Fact]
    public void DbContext_MapujeVsech6HotEntit()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new TicketingReadOnlyDbContext(opts);

        var mappedTypes = db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .ToHashSet();

        mappedTypes.Should().Contain(new[]
        {
            typeof(HotZaznamEntity),
            typeof(HotKalkulaceEntity),
            typeof(HotVyjadreniEntity),
            typeof(HotSubsystemEntity),
            typeof(HotModulyEntity),
            typeof(HotIsEntity),
        }, "TicketingReadOnlyDbContext musí mapovat všech 6 HOT_* entit");
    }
}
