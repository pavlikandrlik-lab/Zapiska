using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Stav úkolu ke dni jednání (spec 2026-10-13 §4.7). Z něj se v tisku jednání odvodí
/// sloupec Stav, pozastavení i ukončení (§4.8). Testy pracují s entitami, databázi nepotřebují.
/// </summary>
public sealed class ExportRecordVisibilityEvaluatorTests
{
    private const int RunningStateId = 1;
    private const int DoneStateId = 2;

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

    private static ZaznamHistorieStavuZaznamuEntity Change(int id, int from, int to, DateTime platiOd, DateTime? savedAt = null) => new()
    {
        Id = id,
        ZaznamId = 100,
        PuvodniStav = from,
        NovyStav = to,
        DatumZmeny = savedAt ?? platiOd,
        PlatiOd = platiOd
    };

    /// <summary>Stejné řazení, v jakém historii předává BuildExportRecordsAsync.</summary>
    private static IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> NewestFirst(params ZaznamHistorieStavuZaznamuEntity[] changes)
        => changes.OrderByDescending(x => x.PlatiOd).ThenByDescending(x => x.Id).ToList();

    [Fact]
    public void ResolveStatus_ReturnsPreviousState_WhenChangeTakesEffectAfterTheMeeting()
    {
        var history = NewestFirst(Change(1, RunningStateId, DoneStateId, new DateTime(2026, 8, 20)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 6, 10))
            .Should().Be(RunningStateId, "na červnovém zápise úkol ještě běžel");
    }

    [Fact]
    public void ResolveStatus_ReturnsNewState_WhenChangeTookEffectBeforeTheMeeting()
    {
        var history = NewestFirst(Change(1, RunningStateId, DoneStateId, new DateTime(2026, 8, 20)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 9, 1))
            .Should().Be(DoneStateId);
    }

    [Fact]
    public void ResolveStatus_CountsChangeEffectiveOnTheMeetingDayAsApplied()
    {
        var history = NewestFirst(Change(1, RunningStateId, DoneStateId, new DateTime(2026, 8, 20)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 8, 20))
            .Should().Be(DoneStateId, "změna platná v den jednání se počítá jako platná");
    }

    [Fact]
    public void ResolveStatus_UsesPlatiOd_NotTheMomentOfSaving()
    {
        var history = NewestFirst(Change(
            1, RunningStateId, DoneStateId, platiOd: new DateTime(2026, 10, 8), savedAt: new DateTime(2026, 10, 13, 7, 0, 0)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 10, 8))
            .Should().Be(DoneStateId);
    }

    /// <summary>
    /// Spec 2026-10-13 §4.4 — omylem ukončeno k 889 a znovuotevřeno zase k 889: obě změny
    /// platí týž den, rozhoduje pořadí uložení a úkol nikde nevyjde jako ukončený.
    /// </summary>
    [Fact]
    public void ResolveStatus_SameDayCloseAndReopen_LastSavedWins()
    {
        var history = NewestFirst(
            Change(1, RunningStateId, DoneStateId, new DateTime(2026, 10, 22)),
            Change(2, DoneStateId, RunningStateId, new DateTime(2026, 10, 22)));
        var evaluator = new ExportRecordVisibilityEvaluator();

        evaluator.ResolveStatusForMeetingPrint(Record(RunningStateId), history, new DateTime(2026, 10, 22))
            .Should().Be(RunningStateId);
        evaluator.ResolveStatusForMeetingPrint(Record(RunningStateId), history, new DateTime(2026, 10, 8))
            .Should().Be(RunningStateId);
    }

    [Fact]
    public void ResolveStatus_ReturnsNull_WhenRecordHasNoTaskState()
        => new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(null), [], new DateTime(2026, 9, 1))
            .Should().BeNull("informace a rozhodnutí nemají stav úkolu");
}
