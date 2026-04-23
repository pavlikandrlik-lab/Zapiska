using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotModulyEntityShapeTests
{
    [Fact]
    public void HotModulyEntity_MaPozadovaneProperty()
    {
        var t = typeof(HotModulyEntity);
        var required = new[] { "Id", "Modul", "Zkratka", "Subsystem", "IdIS", "Faze", "Aktivita", "Dodavatel" };
        foreach (var n in required)
            t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Should().NotBeNull($"property {n}");
    }
}
