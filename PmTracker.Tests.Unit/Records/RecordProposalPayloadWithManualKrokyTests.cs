using System.Text.Json;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordProposalPayloadWithManualKrokyTests
{
    [Fact]
    public void SchedulePlanProposalPayload_ShouldSerializeManualActualKroky()
    {
        var krokKey = Guid.NewGuid();
        var payload = new SchedulePlanProposalPayload
        {
            ProjektId = 1,
            ZaznamId = 42,
            ManualActualKroky = new List<ManualActualKrokDto>
            {
                new() { KrokKey = krokKey, AbsolutniDatum = new DateOnly(2026, 3, 15) }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var roundtripped = JsonSerializer.Deserialize<SchedulePlanProposalPayload>(json);

        roundtripped!.ManualActualKroky.Should().HaveCount(1);
        roundtripped.ManualActualKroky[0].KrokKey.Should().Be(krokKey);
        roundtripped.ManualActualKroky[0].AbsolutniDatum.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void CreateRecordProposalPayload_ShouldSerializeManualActualKroky()
    {
        var krokKey = Guid.NewGuid();
        var payload = new CreateRecordProposalPayload
        {
            ProjektId = 1,
            ManualActualKroky = new List<ManualActualKrokDto>
            {
                new() { KrokKey = krokKey, AbsolutniDatum = new DateOnly(2026, 2, 1) }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var rt = JsonSerializer.Deserialize<CreateRecordProposalPayload>(json);

        rt!.ManualActualKroky.Should().HaveCount(1);
        rt.ManualActualKroky[0].KrokKey.Should().Be(krokKey);
        rt.ManualActualKroky[0].AbsolutniDatum.Should().Be(new DateOnly(2026, 2, 1));
    }

    [Fact]
    public void CreateRecordProposalPayload_ShouldSerializeHarmonogramVazby()
    {
        var krokKey = Guid.NewGuid();
        var payload = new CreateRecordProposalPayload
        {
            ProjektId = 1,
            HarmonogramVazby = new List<HarmonogramVazbaDto>
            {
                new()
                {
                    KrokKey = krokKey,
                    ExterniOdkazIndex = 0,
                    HotVyjadreniId = 99999,
                    DatumVyjadreni = new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.Zero)
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var rt = JsonSerializer.Deserialize<CreateRecordProposalPayload>(json);

        rt!.HarmonogramVazby.Should().HaveCount(1);
        rt.HarmonogramVazby[0].KrokKey.Should().Be(krokKey);
        rt.HarmonogramVazby[0].ExterniOdkazIndex.Should().Be(0);
        rt.HarmonogramVazby[0].HotVyjadreniId.Should().Be(99999);
        rt.HarmonogramVazby[0].DatumVyjadreni.Should()
            .Be(new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ManualActualKrokDto_Default_ShouldBeMinDate()
    {
        var dto = new ManualActualKrokDto();
        dto.AbsolutniDatum.Should().Be(default(DateOnly));
    }
}
