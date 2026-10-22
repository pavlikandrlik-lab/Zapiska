using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-22 R10, R12: poloha záznamu v seznamu, kraje bez přetáčení.</summary>
public sealed class RecordSequencePositionTests
{
    private static readonly RecordSequenceEntry A = new(10, "901-1", "Alfa");
    private static readonly RecordSequenceEntry B = new(20, "901-2", "Beta");
    private static readonly RecordSequenceEntry C = new(30, "7", "Gama");
    private static readonly IReadOnlyList<RecordSequenceEntry> Abc = [A, B, C];

    [Fact]
    public void Middle_HasBothNeighbours()
        => RecordSequencePosition.Locate(Abc, 20).Should().Be(new RecordSequencePosition(1, 3, A, C));

    [Fact]
    public void First_HasNoPrevious_LastHasNoNext()
    {
        RecordSequencePosition.Locate(Abc, 10)!.Previous.Should().BeNull();
        RecordSequencePosition.Locate(Abc, 30)!.Next.Should().BeNull();
    }

    [Fact]
    public void Single_HasNoNeighbours()
        => RecordSequencePosition.Locate([B], 20).Should().Be(new RecordSequencePosition(0, 1, null, null));

    [Fact]
    public void Missing_IsNull()
        => RecordSequencePosition.Locate(Abc, 99).Should().BeNull("záznam mimo seznam nemá šipky (R12)");

    [Fact]
    public void Label_LikeCardTitle()
        => A.Label.Should().Be("#901-1 - Alfa");
}
