using FluentAssertions;
using PmTracker.Web.Services.Common;
using Xunit;

namespace PmTracker.Tests.Unit.Common;

/// <summary>Spec 2026-10-22 R5, R7: pořadí zápisu — jediný zdroj pro tisk i procházení šipkami.</summary>
public sealed class MinutesRecordOrderingTests
{
    private sealed record Row(int Id, string Subsystem, int Poradi, bool HasOrder, string Kategorie, int A, int B = 0)
    {
        public MinutesOrderKey Key => new(Subsystem, Subsystem, Poradi, HasOrder, Kategorie, A, B, Id);
    }

    private static readonly Row[] Subsystems =
    [
        new(1, "Alfa", 0, false, "Úkol", 1),
        new(2, "Zeta", 1, true, "Úkol", 2),
        new(3, "Beta", 2, true, "Úkol", 3),
        new(4, "Gama", 0, false, "Úkol", 4)
    ];

    [Fact]
    public void Groups_ProjectOrderFirst_ThenUnorderedAlphabetically()
        => MinutesRecordOrdering.GroupBySubsystem(Subsystems, r => r.Key, useProjectOrder: true)
            .Select(g => g.Subsystem).Should().Equal("Zeta", "Beta", "Alfa", "Gama");

    [Fact]
    public void WithoutProjectOrder_GroupsAlphabetically()
        => MinutesRecordOrdering.GroupBySubsystem(Subsystems, r => r.Key, useProjectOrder: false)
            .Select(g => g.Subsystem).Should().Equal("Alfa", "Beta", "Gama", "Zeta");

    [Fact]
    public void WithinGroup_CategoryThenVisibleNumber()
    {
        var rows = new[]
        {
            new Row(1, "S", 1, true, "Úkol", 5),
            new Row(2, "S", 1, true, "Informace", 9),
            new Row(3, "S", 1, true, "Rozhodnutí", 7),
            new Row(4, "S", 1, true, "Úkol", 5, B: 1),
            new Row(5, "S", 1, true, "Úkol", 2)
        };

        MinutesRecordOrdering.InMinutesOrder(rows, r => r.Key).Select(r => r.Id)
            .Should().Equal(2, 3, 5, 1, 4);
    }
}
