using System.Reflection;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotZaznamEntityShapeTests
{
    private static Type GetEntityType()
    {
        var asm = Assembly.Load("PmTracker.ServiceDesk.Sql");
        var t = asm.GetType("PmTracker.ServiceDesk.Sql.Entities.HotZaznamEntity", throwOnError: true);
        return t!;
    }

    [Fact]
    public void HotZaznamEntity_ShouldExposePidProperty()
    {
        var prop = GetEntityType().GetProperty("Pid", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        prop.Should().NotBeNull("pid je FK používaný pro join s HOT_VYJADRENI a HOT_KALKULACE");
        prop!.PropertyType.Should().Be(typeof(string), "pid je alfanumerický identifikátor (např. A400P023RVVP)");
    }

    [Fact]
    public void HotZaznamEntity_StavShouldBeString()
    {
        var prop = GetEntityType().GetProperty("Stav", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        prop.Should().NotBeNull();
        var type = Nullable.GetUnderlyingType(prop!.PropertyType) ?? prop.PropertyType;
        type.Should().Be(typeof(string));
    }

    [Fact]
    public void HotZaznamEntity_ShouldExposeDatumProperty()
    {
        var prop = GetEntityType().GetProperty("Datum", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        prop.Should().NotBeNull("datum je zdroj pro krok K1 (založení)");
        var type = Nullable.GetUnderlyingType(prop!.PropertyType) ?? prop.PropertyType;
        type.Should().Be(typeof(DateTime));
    }
}
