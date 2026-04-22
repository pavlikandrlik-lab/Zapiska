using System.Reflection;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotVyjadreniEntityTests
{
    private static Type GetEntityType()
    {
        var asm = Assembly.Load("PmTracker.ServiceDesk.Sql");
        var t = asm.GetType("PmTracker.ServiceDesk.Sql.Entities.HotVyjadreniEntity", throwOnError: true);
        return t!;
    }

    [Fact]
    public void HotVyjadreniEntity_ShouldExposeAllExpectedColumns()
    {
        var t = GetEntityType();
        var expected = new[] { "Id", "Typ", "Pid", "Datum", "Zpracoval", "Popis", "Tym", "ViditelneDodavateli" };
        foreach (var name in expected)
        {
            t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                .Should().NotBeNull($"property {name} má existovat (HOT_VYJADRENI schema)");
        }
    }

    [Fact]
    public void HotVyjadreniEntity_CanBeInstantiatedViaReflection()
    {
        var t = GetEntityType();
        var instance = Activator.CreateInstance(t, nonPublic: true);
        instance.Should().NotBeNull();
    }
}
