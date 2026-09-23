using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Podbarvení ukončených úkolů (2026-09-05): v tisku jednání se ukončenost bere
/// ke dni toho jednání, ne z dneška (rozhodnutí U5). Testy pracují s entitami,
/// databázi nepotřebují.
/// </summary>
public sealed class ExportRecordVisibilityEvaluatorTests
{
    private const int RunningStateId = 1;
    private const int DoneStateId = 2;
    private const int PausedButFinalStateId = 3;

    private static readonly Dictionary<int, CiselnikStavuUkoluEntity> TaskStates = new()
    {
        [RunningStateId] = new CiselnikStavuUkoluEntity
            { Id = RunningStateId, Kod = "RUN", Nazev = "Rozpracováno", IsFinal = false },
        [DoneStateId] = new CiselnikStavuUkoluEntity
            { Id = DoneStateId, Kod = "DONE", Nazev = "Ukončeno", IsFinal = true },
        // Rozporný řádek, jaký může v databázi vzniknout ručním zásahem.
        [PausedButFinalStateId] = new CiselnikStavuUkoluEntity
            { Id = PausedButFinalStateId, Kod = "PAUSE", Nazev = "Pozastaveno", IsFinal = true }
    };

    private static ProjektovyZaznamEntity Record(int? currentStateId) => new()
    {
        Id = 100,
        ProjektId = 1,
        KategorieId = 1,
        SubsystemId = 1,
        VlastnikId = 1,
        CisloZaznamu = 1,
        Nazev = "Testovací úkol",
        StavUkoluId = currentStateId,
        DatumZalozeni = new DateTime(2026, 1, 5)
    };

    /// <summary>Úkol byl 20.8. přepnut z Rozpracováno na Ukončeno.</summary>
    private static IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> ClosedOn20August() =>
    [
        new()
        {
            Id = 1,
            ZaznamId = 100,
            PuvodniStav = RunningStateId,
            NovyStav = DoneStateId,
            DatumZmeny = new DateTime(2026, 8, 20)
        }
    ];

    [Fact]
    public void IsCompletedForMeetingPrint_IsFalse_WhenTaskWasClosedAfterTheMeeting()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(DoneStateId), TaskStates, ClosedOn20August(), new DateTime(2026, 6, 10));

        result.Should().BeFalse(
            "na červnovém zápise úkol ještě běžel — dnešní stav se do historického tisku promítnout nesmí");
    }

    [Fact]
    public void IsCompletedForMeetingPrint_IsTrue_WhenTaskWasAlreadyClosedAtTheMeeting()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(DoneStateId), TaskStates, ClosedOn20August(), new DateTime(2026, 9, 1));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCompletedForMeetingPrint_CountsChangeOnTheMeetingDayAsAlreadyApplied()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(DoneStateId), TaskStates, ClosedOn20August(), new DateTime(2026, 8, 20));

        result.Should().BeTrue("stav se bere ke konci dne jednání");
    }

    [Fact]
    public void IsCompletedForMeetingPrint_IsFalse_ForPausedStateMarkedFinal()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(PausedButFinalStateId), TaskStates, [], new DateTime(2026, 9, 1));

        result.Should().BeFalse("rozhodnutí U1 — pozastavení nikdy neplatí za ukončení");
    }

    [Fact]
    public void IsCompletedForMeetingPrint_IsFalse_WhenRecordHasNoTaskState()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(null), TaskStates, [], new DateTime(2026, 9, 1));

        result.Should().BeFalse("informace a rozhodnutí nemají stav úkolu, nemohou být ukončené");
    }
}
