using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordProposalPayloadMapperTests
{
    private readonly RecordProposalPayloadMapper _sut = new();

    [Fact]
    public void BuildSchedulePayload_ShouldSplitPlannedAndActualValues_AndSetChangedFlags()
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

        var payload = _sut.BuildSchedulePayload(
            command,
            plannedTypeIds: [101, 103],
            actualTypeIds: [102],
            originalDeadline: new DateTime(2026, 4, 25),
            existingValues: new Dictionary<int, int>
            {
                [101] = 4,
                [102] = 1,
                [103] = 9
            });

        payload.ProposalType.Should().Be(RecordProposalTypeCodes.SchedulePlanChange);
        payload.SchedulePlan.Should().NotBeNull();
        payload.SchedulePlan!.ProjektId.Should().Be(17);
        payload.SchedulePlan.ZaznamId.Should().Be(19);
        payload.SchedulePlan.PlannedHarmonogramHodnoty.Should().BeEquivalentTo(
        [
            new { TypId = 101, Hodnota = 5 },
            new { TypId = 103, Hodnota = 9 }
        ]);
        payload.SchedulePlan.ActualHarmonogramHodnoty.Should().BeEquivalentTo(
        [
            new { TypId = 102, Hodnota = 2 }
        ]);
        payload.SchedulePlan.ChangesTermDeadline.Should().BeTrue();
        payload.SchedulePlan.ChangesSchedulePlan.Should().BeTrue();
        payload.SchedulePlan.ChangesScheduleActual.Should().BeTrue();
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

    [Fact]
    public void BuildCreatePayload_ShouldRoundTripManualActualKrokyAndVazby()
    {
        var krokKey1 = Guid.NewGuid();
        var krokKey2 = Guid.NewGuid();
        var command = new SaveRecordCommand
        {
            ProjektId = 1,
            Kategorie = "Úkol",
            Stav = "Nový",
            Nazev = "N1",
            VlastnikId = 10,
            Subsystem = "SYS",
            DatumZalozeni = new DateTime(2026, 1, 1),
            TerminUkonceni = new DateTime(2026, 6, 1),
            ManualActualKroky =
            [
                new ManualActualKrokDto { KrokKey = krokKey1, AbsolutniDatum = new DateOnly(2026, 3, 1) }
            ],
            HarmonogramVazby =
            [
                new HarmonogramVazbaDto
                {
                    KrokKey = krokKey2,
                    ExterniOdkazIndex = 1,
                    HotVyjadreniId = 555,
                    DatumVyjadreni = new DateTimeOffset(2026, 3, 14, 10, 0, 0, TimeSpan.Zero)
                }
            ]
        };

        var payload = _sut.BuildCreatePayload(command);
        var roundtripped = _sut.BuildSaveCommand(payload.CreateRecord!);

        roundtripped.ManualActualKroky.Should().HaveCount(1);
        roundtripped.ManualActualKroky[0].KrokKey.Should().Be(krokKey1);
        roundtripped.ManualActualKroky[0].AbsolutniDatum.Should().Be(new DateOnly(2026, 3, 1));
        roundtripped.HarmonogramVazby.Should().HaveCount(1);
        roundtripped.HarmonogramVazby[0].KrokKey.Should().Be(krokKey2);
        roundtripped.HarmonogramVazby[0].ExterniOdkazIndex.Should().Be(1);
        roundtripped.HarmonogramVazby[0].HotVyjadreniId.Should().Be(555);
    }

    [Fact]
    public void BuildSchedulePayload_ShouldRoundTripManualActualKroky()
    {
        var krokKey = Guid.NewGuid();
        var command = new SaveRecordCommand
        {
            ProjektId = 1,
            Id = 19,
            TerminUkonceni = new DateTime(2026, 4, 30),
            ManualActualKroky =
            [
                new ManualActualKrokDto { KrokKey = krokKey, AbsolutniDatum = new DateOnly(2026, 4, 10) }
            ]
        };

        var payload = _sut.BuildSchedulePayload(
            command,
            plannedTypeIds: [101],
            actualTypeIds: [102],
            originalDeadline: new DateTime(2026, 4, 30),
            existingValues: new Dictionary<int, int>());

        payload.SchedulePlan!.ManualActualKroky.Should().HaveCount(1);
        payload.SchedulePlan.ManualActualKroky[0].KrokKey.Should().Be(krokKey);
        payload.SchedulePlan.ManualActualKroky[0].AbsolutniDatum.Should().Be(new DateOnly(2026, 4, 10));
    }
}
