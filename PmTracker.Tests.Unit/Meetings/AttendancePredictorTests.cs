using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Meetings;
using Xunit;

namespace PmTracker.Tests.Unit.Meetings;

/// <summary>
/// Odhad předvyplněné účasti u nového jednání — spec
/// docs/superpowers/specs/2026-09-05-predvyplneni-ucasti-design.md.
/// Testuje pravidla rozhodování bez databáze: nejčetnější stav, strop 15 jednání,
/// jen uzavřená jednání, rozhodnutí shody počtů, osoba bez historie, cizí projekt.
/// </summary>
public sealed class AttendancePredictorTests
{
    private const int ProjektId = 7;
    private const int JinyProjektId = 8;
    private const int MemberOsobaId = 100;
    private const int NewcomerOsobaId = 101;

    private const int PresentStateId = 1;
    private const int OnlineStateId = 2;
    private const int ExcusedStateId = 3;

    private const int ClosedMeetingStateId = 10;
    private const int DraftMeetingStateId = 11;
    private const int OpenMeetingStateId = 12;

    private static PmTrackerDbContext CreateDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("attendance-predictor-" + Guid.NewGuid())
            .Options);

    private static async Task SeedMeetingStatesAsync(PmTrackerDbContext db)
    {
        db.CiselnikStavuJednani.AddRange(
            new CiselnikStavuJednaniEntity { Id = ClosedMeetingStateId, Kod = "CLOSED", Nazev = "Uzavřeno" },
            new CiselnikStavuJednaniEntity { Id = DraftMeetingStateId, Kod = "DRAFT", Nazev = "Příprava" },
            new CiselnikStavuJednaniEntity { Id = OpenMeetingStateId, Kod = "OPEN", Nazev = "Otevřeno pro zápis" });
        await db.SaveChangesAsync();
    }

    /// <summary>Id jednání = jeho číslo; testy tak nemusí držet dvě číselné řady.</summary>
    private static void AddMeeting(
        PmTrackerDbContext db, int meetingNumber, int stateId, int projectId = ProjektId)
    {
        db.Jednani.Add(new JednaniEntity
        {
            Id = meetingNumber,
            ProjektId = projectId,
            CisloJednani = meetingNumber,
            DatumPlanovane = new DateTime(2026, 1, 1).AddDays(meetingNumber),
            CasZacatek = new TimeOnly(9, 0),
            StavJednaniId = stateId
        });
    }

    private static void AddAttendance(PmTrackerDbContext db, int meetingNumber, int osobaId, int stateId)
        => db.Ucast.Add(new UcastEntity
        {
            JednaniId = meetingNumber,
            OsobaId = osobaId,
            StavUcastiId = stateId
        });

    [Fact]
    public async Task PredictAsync_VybereNejcetnejsiStav()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        for (var meetingNumber = 1; meetingNumber <= 10; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId);
            AddAttendance(db, meetingNumber, MemberOsobaId,
                meetingNumber <= 7 ? OnlineStateId : PresentStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(OnlineStateId,
            "7 z 10 uzavřených jednání bylo přes videokonferenci");
    }

    [Fact]
    public async Task PredictAsync_PocitaZTohoCoJe_KdyzJednaniJeMeneNezStrop()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        AddMeeting(db, meetingNumber: 2, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 2, MemberOsobaId, ExcusedStateId);
        AddMeeting(db, meetingNumber: 3, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 3, MemberOsobaId, ExcusedStateId);
        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(ExcusedStateId,
            "tři dostupná jednání se počítají stejně jako plných patnáct (spec U2)");
    }

    [Fact]
    public async Task PredictAsync_OsobaBezHistorieVeVysledkuNeni()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db)
            .PredictAsync(ProjektId, [MemberOsobaId, NewcomerOsobaId]);

        result.Should().ContainKey(MemberOsobaId);
        result.Should().NotContainKey(NewcomerOsobaId,
            "pro osobu bez historie volající použije výchozí stav, odhad ji nevymýšlí");
    }

    [Fact]
    public async Task PredictAsync_IgnorujeJednaniJinehoProjektu()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        for (var meetingNumber = 2; meetingNumber <= 6; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId, JinyProjektId);
            AddAttendance(db, meetingNumber, MemberOsobaId, OnlineStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(PresentStateId,
            "pět videokonferencí v jiném projektu nesmí přebít jediné jednání v tomto");
    }

    [Fact]
    public async Task PredictAsync_IgnorujeJednaniVPripraveAOtevrena()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        AddMeeting(db, meetingNumber: 2, DraftMeetingStateId);
        AddAttendance(db, meetingNumber: 2, MemberOsobaId, OnlineStateId);
        AddMeeting(db, meetingNumber: 3, OpenMeetingStateId);
        AddAttendance(db, meetingNumber: 3, MemberOsobaId, OnlineStateId);
        AddMeeting(db, meetingNumber: 4, OpenMeetingStateId);
        AddAttendance(db, meetingNumber: 4, MemberOsobaId, OnlineStateId);
        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(PresentStateId,
            "účast u neuzavřených jednání může být jen předvyplněná odhadem — jinak by si odhad potvrzoval sám sebe (spec U3)");
    }

    [Fact]
    public async Task PredictAsync_ZapocitaJenPoslednichPatnactJednani()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);

        // Jednání 1-12: Přítomen (12×), jednání 13-20: Videokonference (8×).
        // Bez stropu vyhraje Přítomen 12:8. V posledních patnácti (6-20) je to
        // 7× Přítomen proti 8× Videokonference → musí vyhrát Videokonference.
        for (var meetingNumber = 1; meetingNumber <= 20; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId);
            AddAttendance(db, meetingNumber, MemberOsobaId,
                meetingNumber <= 12 ? PresentStateId : OnlineStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(OnlineStateId,
            "starší jednání než posledních patnáct se nezapočítávají (spec U2)");
    }

    [Fact]
    public async Task PredictAsync_PriShodePoctuVyhrajeStavZNejnovejsihoJednani()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);

        // 2:2 — Omluven u jednání 1 a 2, Videokonference u 3 a 4.
        // Řádky se zakládají od nejstaršího, aby implementace bez rozhodnutí shody
        // sáhla po Omluven (skupina, na kterou narazí první) a test měl červený běh.
        for (var meetingNumber = 1; meetingNumber <= 4; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId);
            AddAttendance(db, meetingNumber, MemberOsobaId,
                meetingNumber <= 2 ? ExcusedStateId : OnlineStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(OnlineStateId,
            "při shodě počtů rozhoduje, co osoba dělala naposledy (spec §4.2)");
    }
}
