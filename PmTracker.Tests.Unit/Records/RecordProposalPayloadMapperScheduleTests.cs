using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Task 3 (2026-10-12): pole „Důvod změny" v návrhu změny termínu a harmonogramu.
/// Nepovinné sdělení autora návrhu schvalovateli — ořezává whitespace, prázdné/whitespace
/// vstupy mapuje na null (payload bez důvodu == starší návrhy bez pole).
/// </summary>
public sealed class RecordProposalPayloadMapperScheduleTests
{
    private static readonly IReadOnlySet<int> BezPlanu = new HashSet<int>();

    private static SaveRecordCommand Command(string? duvod) => new()
    {
        Id = 5, ProjektId = 1, TerminUkonceni = new DateTime(2026, 11, 30), DuvodZmeny = duvod
    };

    [Theory]
    [InlineData("  Dodavatel posunul dodávku.  ", "Dodavatel posunul dodávku.")]
    [InlineData("Řádek 1\nŘádek 2", "Řádek 1\nŘádek 2")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void BuildSchedulePayload_PrenasiDuvodZmeny_OrezanyNeboNull(string? vstup, string? ocekavany)
    {
        var payload = new RecordProposalPayloadMapper().BuildSchedulePayload(Command(vstup), new DateTime(2026, 10, 31), currentAutoFill: true, currentPlanSteps: BezPlanu);
        payload.SchedulePlan!.DuvodZmeny.Should().Be(ocekavany);
    }

    /// <summary>
    /// Task 5 (2026-10-12): přepínač „Automatické vyplňování harmonogramu" se ukládá do návrhu
    /// (HarmonogramRezim) spolu s tím, zda proti aktuálnímu stavu záznamu (currentAutoFill)
    /// jde o skutečnou změnu (ChangesHarmonogramRezim) — řídí schválení i lock pending návrhu.
    /// </summary>
    [Theory]
    [InlineData("Manual", true, "Manual", true)]
    [InlineData("Auto", true, "Auto", false)]
    [InlineData("Auto", false, "Auto", true)]
    [InlineData(null, true, null, false)]
    public void BuildSchedulePayload_NeseRezimAZmenu(string? rezim, bool soucasneAuto, string? ocekavanyRezim, bool ocekavanaZmena)
    {
        var command = Command(null);
        command.HarmonogramRezim = rezim;
        var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), soucasneAuto, currentPlanSteps: BezPlanu).SchedulePlan!;
        plan.HarmonogramRezim.Should().Be(ocekavanyRezim);
        plan.ChangesHarmonogramRezim.Should().Be(ocekavanaZmena);
    }

    private static SaveRecordCommand CommandSeSkutecnosti(string rezim, params (int Poradi, DateTime? Datum, DateTime? Puvodni)[] kroky)
        => new()
        {
            TerminUkonceni = new DateTime(2026, 10, 31),
            HarmonogramRezim = rezim,
            HarmonogramHodnoty = kroky.Select(k => new SaveRecordHarmonogramValueCommand
            {
                Poradi = k.Poradi, SkutecnostDatum = k.Datum, SkutecnostDatumPuvodni = k.Puvodni
            }).ToList()
        };

    [Fact]
    public void BuildSchedulePayload_SkutecnostJeSeznamZmen()
    {
        var a = new DateTime(2026, 10, 5);
        var command = CommandSeSkutecnosti("Manual", (2, a, a), (3, null, a), (5, a, null));

        var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: false, currentPlanSteps: BezPlanu).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Select(x => (x.Poradi, x.SkutecnostDatum)).Should().BeEquivalentTo(new[]
        {
            (3, (DateTime?)null),   // smazáno
            (5, (DateTime?)a)       // nově vyplněno; krok 2 beze změny se nepošle
        });
        plan.ChangesScheduleActual.Should().BeTrue();
    }

    /// <summary>Review Focus 4 — přepnutí na automatiku v návrhu vyčistí vstupy; to nejsou změny.</summary>
    [Fact]
    public void BuildSchedulePayload_CilAuto_VynechaZmenyAutoKroku()
    {
        var a = new DateTime(2026, 10, 5);
        var command = CommandSeSkutecnosti("Auto", (3, null, a), (5, a, null));

        var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: false, currentPlanSteps: BezPlanu).SchedulePlan!;

        plan.ActualHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 5,
            "v automatickém režimu se kroky 1/3/4/6/7/10 neposílají, kroky 2/5/8/9 ano");
    }

    /// <summary>
    /// Spec 2026-10-22 R14: návrh nese odstranění plánu — krok, který plán v záznamu má a ve
    /// formuláři přišel prázdný. Krok bez plánu, který zůstal prázdný, změnou není (jinak by
    /// každý návrh „měnil“ plán všech nevyplněných kroků).
    /// </summary>
    [Fact]
    public void BuildSchedulePayload_NeseOdstraneniPlanu_PrazdnyKrokBezPlanuNe()
    {
        var a = new DateTime(2026, 10, 5);
        var command = new SaveRecordCommand
        {
            TerminUkonceni = new DateTime(2026, 10, 31),
            HarmonogramHodnoty =
            [
                new SaveRecordHarmonogramValueCommand { Poradi = 1, PlanDatum = a },
                new SaveRecordHarmonogramValueCommand { Poradi = 2, PlanDatum = null },
                new SaveRecordHarmonogramValueCommand { Poradi = 3, PlanDatum = null }
            ]
        };

        var plan = new RecordProposalPayloadMapper()
            .BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: true, currentPlanSteps: new HashSet<int> { 1, 2 })
            .SchedulePlan!;

        plan.PlannedHarmonogramHodnoty.Select(x => (x.Poradi, x.PlanDatum)).Should().Equal(
            (1, (DateTime?)a),
            (2, (DateTime?)null));
        plan.ChangesSchedulePlan.Should().BeTrue();
    }

    [Fact]
    public void BuildSchedulePayload_JenOdstraneniPlanu_JeZmenaPlanu()
    {
        var command = new SaveRecordCommand
        {
            TerminUkonceni = new DateTime(2026, 10, 31),
            HarmonogramHodnoty = [new SaveRecordHarmonogramValueCommand { Poradi = 4, PlanDatum = null }]
        };

        var plan = new RecordProposalPayloadMapper()
            .BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: true, currentPlanSteps: new HashSet<int> { 4 })
            .SchedulePlan!;

        plan.ChangesSchedulePlan.Should().BeTrue("návrh, který jen maže plán, není prázdný návrh");
        plan.PlannedHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 4 && x.PlanDatum == null);
    }
}
