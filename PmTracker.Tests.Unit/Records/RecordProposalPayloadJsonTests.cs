using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-14 R4 — starý JSON návrhu (ManualActualKroky) se čte převedený.</summary>
public sealed class RecordProposalPayloadJsonTests
{
    [Fact]
    public void StaryFormat_RucniDataSePrevedouNaZmenySkutecnosti()
    {
        const string json = """
            {"ProposalType":"SCHEDULE_PLAN_CHANGE","SchedulePlan":{"ZaznamId":7,
             "ActualHarmonogramHodnoty":[{"Poradi":4,"SkutecnostDatum":"2026-10-01T00:00:00"}],
             "ManualActualKroky":[{"Poradi":3,"AbsolutniDatum":"2026-10-05","PreferredZdroj":null},
                                  {"Poradi":5,"AbsolutniDatum":null},
                                  {"Poradi":4,"AbsolutniDatum":"2026-10-09"}]}}
            """;

        var plan = RecordProposalPayloadJson.Deserialize(json).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Select(x => (x.Poradi, x.SkutecnostDatum)).Should().BeEquivalentTo(new[]
        {
            (4, (DateTime?)new DateTime(2026, 10, 1)),   // existující položka vyhrává
            (3, (DateTime?)new DateTime(2026, 10, 5))    // prázdné AbsolutniDatum = „krok nenastal“, ne smazání
        });
    }

    [Fact]
    public void NovyFormat_CteSeBezeZmeny()
    {
        const string json = """
            {"ProposalType":"SCHEDULE_PLAN_CHANGE","SchedulePlan":{"ZaznamId":7,
             "ActualHarmonogramHodnoty":[{"Poradi":3,"SkutecnostDatum":null}],"ChangesScheduleActual":true}}
            """;

        var plan = RecordProposalPayloadJson.Deserialize(json).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 3 && x.SkutecnostDatum == null,
            "null v seznamu změn znamená smazat");
    }

    [Fact]
    public void PrazdnyJson_VratiPrazdnyPayload()
        => RecordProposalPayloadJson.Deserialize("  ").SchedulePlan.Should().BeNull();
}
