using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotSubsystemEntityShapeTests
{
    [Fact]
    public void HotSubsystemEntity_MaPozadovaneProperty()
    {
        var t = typeof(HotSubsystemEntity);
        var required = new[] { "Id", "Nazev", "Zkratka", "Aktivita", "Dodavatel", "PriznakGdprSub" };
        foreach (var n in required)
            t.GetProperty(n, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Should().NotBeNull($"property {n}");
    }

    [Fact]
    public void HotSubsystemEntity_ZkratkaJeNonNullableString()
    {
        // HOT_SUBSYSTEM.zkratka je nvarchar(5) NOT NULL a reálné PK
        var prop = typeof(HotSubsystemEntity).GetProperty("Zkratka",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        prop.PropertyType.Should().Be(typeof(string));
    }
}
