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
        var payload = new RecordProposalPayloadMapper().BuildSchedulePayload(Command(vstup), new DateTime(2026, 10, 31));
        payload.SchedulePlan!.DuvodZmeny.Should().Be(ocekavany);
    }
}
