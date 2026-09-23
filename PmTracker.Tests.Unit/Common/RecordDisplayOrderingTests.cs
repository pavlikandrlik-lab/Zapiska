using System;
using System.Linq;
using FluentAssertions;
using PmTracker.Web.Services.Common;
using Xunit;

namespace PmTracker.Tests.Unit.Common;

public sealed class RecordDisplayOrderingTests
{
    [Theory]
    [InlineData("Informace", 1)]
    [InlineData("Rozhodnutí", 2)]
    [InlineData("Úkol", 3)]
    [InlineData("Ukol", 3)]
    [InlineData("Něco jiného", 4)]
    [InlineData("", 4)]
    [InlineData(null, 4)]
    public void CategoryOrder_MapsCategoryToPrimaryRank(string? name, int expected)
        => RecordDisplayOrdering.CategoryOrder(name).Should().Be(expected);

    [Theory]
    [InlineData(873, 5, 873)] // má viditelné číslo → to
    [InlineData(0, 5, 5)]     // nemá → fallback CisloZaznamu
    [InlineData(0, 0, 0)]
    public void VisibleNumberPartA_PrefersVisibleNumber_ElseRecordNumber(int a, int cisloZaznamu, int expected)
        => RecordDisplayOrdering.VisibleNumberPartA(a, cisloZaznamu).Should().Be(expected);

    [Theory]
    [InlineData((byte)1, 2, 2)]  // meeting type → pozice
    [InlineData((byte)1, 0, 1)]  // meeting type, prázdné → min 1
    [InlineData((byte)0, 7, 0)]  // ne-meeting → 0
    public void VisibleNumberPartB_OnlyForMeetingType(byte typ, int b, int expected)
        => RecordDisplayOrdering.VisibleNumberPartB(typ, b).Should().Be(expected);

    [Fact]
    public void CompoundSort_CategoryPrimary_ThenVisibleNumber_873Before881_RegardlessOfId()
    {
        var rows = new[]
        {
            (Id: 2, Cat: "Úkol", A: 881, B: 2),
            (Id: 5, Cat: "Informace", A: 873, B: 1),
            (Id: 3, Cat: "Rozhodnutí", A: 873, B: 2),
            (Id: 1, Cat: "Úkol", A: 875, B: 1),
            (Id: 4, Cat: "Informace", A: 881, B: 1),
        };

        var ordered = rows
            .OrderBy(r => RecordDisplayOrdering.CategoryOrder(r.Cat))
            .ThenBy(r => r.Cat, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => RecordDisplayOrdering.VisibleNumberPartA(r.A, 0))
            .ThenBy(r => RecordDisplayOrdering.VisibleNumberPartB(1, r.B))
            .Select(r => r.Id)
            .ToArray();

        // Info(873,881) → Rozhodnutí(873) → Úkol(875,881)
        ordered.Should().Equal(5, 4, 3, 1, 2);
    }
}
