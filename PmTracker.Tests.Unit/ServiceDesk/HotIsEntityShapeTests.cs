using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotIsEntityShapeTests
{
    [Fact]
    public void HotIsEntity_MaPozadovaneProperty()
    {
        var t = typeof(HotIsEntity);
        var required = new[] { "Id", "Nazev", "Zkratka", "Aktivita", "Limit", "Cerpani", "Semafor" };
        foreach (var n in required)
            t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Should().NotBeNull($"property {n}");
    }

    [Fact]
    public void HotIsEntity_LimitACerpaniJsouDecimal()
    {
        var t = typeof(HotIsEntity);
        t.GetProperty("Limit", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.PropertyType.Should().Be(typeof(decimal?));
        t.GetProperty("Cerpani", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.PropertyType.Should().Be(typeof(decimal?));
    }
}
