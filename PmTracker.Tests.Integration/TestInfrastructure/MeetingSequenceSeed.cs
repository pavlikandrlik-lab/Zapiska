using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Spec 2026-10-22 R5–R8 — sdílená data pro tisk zápisu i službu sekvence.
///
/// Projekt má dva subsystémy v pořadí projektu proti abecedě (SEQZZ = 1, SEQAA = 2) a jednání
/// 887 (1. 10. 2026) a 888 (8. 10. 2026). Záznamy, číslo záznamu = pořadí založení:
///   r1 Úkol ZZ, běží, založen 1. 9.
///   r2 Informace ZZ, běží, založen 1. 9.
///   r3 Úkol AA, běží, založen 1. 9.
///   r4 Úkol ZZ, běží, založen 5. 10.
///   r5 Úkol ZZ, běží, založen 20. 10. (po 888)
///   r6 Úkol ZZ, uzavřen 20. 9. (před 887)
///   r7 Úkol AA, uzavřen 5. 10. (mezi 887 a 888)
///   r8 Úkol ZZ, uzavřen 12. 10. (po 888)
/// </summary>
public sealed record MeetingSequenceSeed(
    int AdminId, int ProjectId, int Meeting887, int Meeting888,
    int R1, int R2, int R3, int R4, int R5, int R6, int R7, int R8)
{
    /// <summary>888: ZZ (Informace r2; úkoly r1, r4, r8), pak AA (r3, r7). Mimo jsou r5 (založen po jednání) a r6 (uzavřen před minulým).</summary>
    public IReadOnlyList<int> ExpectedMeeting888 => [R2, R1, R4, R8, R3, R7];

    /// <summary>887 je první jednání (bez minulého). Navíc mimo je r4 (založen až po něm); r7 a r8 k 1. 10. běžely.</summary>
    public IReadOnlyList<int> ExpectedMeeting887 => [R2, R1, R8, R3, R7];

    /// <summary>Aktivní dnes (stav není konečný), bez ohledu na datum založení.</summary>
    public IReadOnlyList<int> ExpectedActive => [R2, R1, R4, R5, R3];

    public static async Task<MeetingSequenceSeed> CreateAsync(PmTrackerDbContext db)
    {
        var adminId = await IntegrationTestHelper.EnsurePersonAsync(db, "SeqAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(db, "SeqOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(db, "SEQPRJ");
        var zz = await IntegrationTestHelper.EnsureSubsystemAsync(db, "SEQZZ", adminId);
        var aa = await IntegrationTestHelper.EnsureSubsystemAsync(db, "SEQAA", adminId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(db, projectId, zz);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(db, projectId, aa);

        var running = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var done = await db.CiselnikStavuUkolu.Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();

        var m887 = await MeetingAsync(db, projectId, 887, new DateTime(2026, 10, 1));
        var m888 = await MeetingAsync(db, projectId, 888, new DateTime(2026, 10, 8));

        var r1 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR1", new DateTime(2026, 9, 1));
        var r2 = await RecordAsync(db, projectId, ownerId, zz, "INFO", "SeqR2", new DateTime(2026, 9, 1));
        var r3 = await RecordAsync(db, projectId, ownerId, aa, "U", "SeqR3", new DateTime(2026, 9, 1));
        var r4 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR4", new DateTime(2026, 10, 5));
        var r5 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR5", new DateTime(2026, 10, 20));
        var r6 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR6", new DateTime(2026, 9, 1));
        var r7 = await RecordAsync(db, projectId, ownerId, aa, "U", "SeqR7", new DateTime(2026, 9, 1));
        var r8 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR8", new DateTime(2026, 9, 1));
        await CloseAsync(db, r6, running, done, new DateTime(2026, 9, 20));
        await CloseAsync(db, r7, running, done, new DateTime(2026, 10, 5));
        await CloseAsync(db, r8, running, done, new DateTime(2026, 10, 12));

        return new MeetingSequenceSeed(adminId, projectId, m887, m888, r1, r2, r3, r4, r5, r6, r7, r8);
    }

    private static async Task<int> MeetingAsync(PmTrackerDbContext db, int projectId, int number, DateTime date)
    {
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(db, projectId, "OPEN", meetingNumber: number);
        var meeting = await db.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = date;
        await db.SaveChangesAsync();
        return meetingId;
    }

    private static async Task<int> RecordAsync(
        PmTrackerDbContext db, int projectId, int ownerId, int subsystemId, string category, string marker, DateTime zalozeni)
    {
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(db, projectId, ownerId, subsystemId, category, marker);
        var record = await db.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = zalozeni;
        await db.SaveChangesAsync();
        return recordId;
    }

    private static async Task CloseAsync(PmTrackerDbContext db, int recordId, int fromStateId, int toStateId, DateTime platiOd)
    {
        var record = await db.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.StavUkoluId = toStateId;
        db.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = fromStateId,
            NovyStav = toStateId,
            DatumZmeny = platiOd,
            PlatiOd = platiOd
        });
        await db.SaveChangesAsync();
    }
}
