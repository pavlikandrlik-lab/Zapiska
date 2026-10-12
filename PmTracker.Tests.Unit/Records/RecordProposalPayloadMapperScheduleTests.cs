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
        var payload = new RecordProposalPayloadMapper().BuildSchedulePayload(Command(vstup), new DateTime(2026, 10, 31), currentAutoFill: true);
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
        var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), soucasneAuto).SchedulePlan!;
        plan.HarmonogramRezim.Should().Be(ocekavanyRezim);
        plan.ChangesHarmonogramRezim.Should().Be(ocekavanaZmena);
    }
}
