using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordProposalPayloadMapperTests
{
    private readonly RecordProposalPayloadMapper _sut = new();

    [Fact]
    public void BuildSchedulePayload_ShouldKeepOnlyPlannedTypeIds()
    {
        var command = new SaveRecordCommand
        {
            ProjektId = 17,
            Id = 19,
            TerminUkonceni = new DateTime(2026, 4, 30),
            HarmonogramHodnoty =
            [
                new SaveRecordHarmonogramValueCommand { TypId = 101, Hodnota = 5 },
                new SaveRecordHarmonogramValueCommand { TypId = 102, Hodnota = 2 },
                new SaveRecordHarmonogramValueCommand { TypId = 103, Hodnota = 9 }
            ]
        };

        var payload = _sut.BuildSchedulePayload(command, [101, 103]);

        payload.ProposalType.Should().Be(RecordProposalTypeCodes.SchedulePlanChange);
        payload.SchedulePlan.Should().NotBeNull();
        payload.SchedulePlan!.ProjektId.Should().Be(17);
        payload.SchedulePlan.ZaznamId.Should().Be(19);
        payload.SchedulePlan.PlannedHarmonogramHodnoty.Should().BeEquivalentTo(
        [
            new { TypId = 101, Hodnota = 5 },
            new { TypId = 103, Hodnota = 9 }
        ]);
    }

    [Fact]
    public void BuildSaveCommand_ShouldRoundTripCreateProposalFields()
    {
        var payload = new CreateRecordProposalPayload
        {
            ProjektId = 11,
            Kategorie = "Úkol",
            TypUkolu = "Interní",
            Stav = "Běží",
            Nazev = "Nový návrh",
            Cil = "Cíl",
            Popis = "<p>Popis</p>",
            VlastnikId = 25,
            DatumZalozeni = new DateTime(2026, 4, 1),
            TerminUkonceni = new DateTime(2026, 4, 30),
            Subsystem = "SYS",
            VybraniSpolupracovniciIds = [3, 4],
            ExterniVazby =
            [
                new SaveRecordExterniVazbaCommand
                {
                    Typ = "OBJ",
                    Cislo = "2026/15"
                }
            ],
            HarmonogramHodnoty =
            [
                new SaveRecordHarmonogramValueCommand { TypId = 21, Hodnota = 8 }
            ],
            JednaniIdProCislo = 91
        };

        var command = _sut.BuildSaveCommand(payload);

        command.ProjektId.Should().Be(11);
        command.Kategorie.Should().Be("Úkol");
        command.TypUkolu.Should().Be("Interní");
        command.Stav.Should().Be("Běží");
        command.Nazev.Should().Be("Nový návrh");
        command.Cil.Should().Be("Cíl");
        command.Popis.Should().Be("<p>Popis</p>");
        command.VlastnikId.Should().Be(25);
        command.DatumZalozeni.Should().Be(new DateTime(2026, 4, 1));
        command.TerminUkonceni.Should().Be(new DateTime(2026, 4, 30));
        command.Subsystem.Should().Be("SYS");
        command.VybraniSpolupracovniciIds.Should().Equal([3, 4]);
        command.ExterniVazby.Should().ContainSingle();
        command.HarmonogramHodnoty.Should().ContainSingle().Which.TypId.Should().Be(21);
        command.JednaniIdProCislo.Should().Be(91);
    }
}
