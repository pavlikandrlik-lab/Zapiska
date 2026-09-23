using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Meetings;

public sealed class AttendancePredictor(PmTrackerDbContext dbContext) : IAttendancePredictor
{
    /// <summary>Kód stavu uzavřeného jednání — stejný identifikátor používá zbytek aplikace
    /// (např. ProjectService.LazyQueries.cs, RecordService.MeetingIdentifier.cs).</summary>
    private const string ClosedMeetingStateCode = "CLOSED";

    /// <summary>Kolik posledních uzavřených jednání se do odhadu započítá (spec U2).</summary>
    private const int MaxConsideredMeetings = 15;

    public async Task<IReadOnlyDictionary<int, int>> PredictAsync(
        int projectId,
        IReadOnlyCollection<int> osobaIds,
        CancellationToken ct = default)
    {
        var empty = new Dictionary<int, int>();
        if (osobaIds.Count == 0)
        {
            return empty;
        }

        // Pořadí podle čísla jednání sestupně — stejné pojetí „předchozího jednání",
        // jaké používá tisk (ExportProjectionBuilders.cs:798). Dvě různá pojetí by mátla.
        var meetingIds = await (
            from meeting in dbContext.Jednani.AsNoTracking()
            join state in dbContext.CiselnikStavuJednani.AsNoTracking()
                on meeting.StavJednaniId equals state.Id
            where meeting.ProjektId == projectId && state.Kod == ClosedMeetingStateCode
            orderby meeting.CisloJednani descending
            select meeting.Id)
            .Take(MaxConsideredMeetings)
            .ToListAsync(ct);
        if (meetingIds.Count == 0)
        {
            return empty;
        }

        var personIds = osobaIds.ToList();
        var attendanceRows = await dbContext.Ucast.AsNoTracking()
            .Where(row => meetingIds.Contains(row.JednaniId) && personIds.Contains(row.OsobaId))
            .Select(row => new { row.JednaniId, row.OsobaId, row.StavUcastiId })
            .ToListAsync(ct);

        // 0 = nejnovější jednání. Bez tohoto pravidla by při shodě počtů rozhodovalo
        // pořadí řádků z databáze a stejné zadání by mohlo dát pokaždé jiný výsledek.
        var recencyRankByMeetingId = meetingIds
            .Select((meetingId, index) => (meetingId, index))
            .ToDictionary(x => x.meetingId, x => x.index);

        return attendanceRows
            .GroupBy(row => row.OsobaId)
            .ToDictionary(
                personRows => personRows.Key,
                personRows => personRows
                    .GroupBy(row => row.StavUcastiId)
                    .OrderByDescending(stateRows => stateRows.Count())
                    .ThenBy(stateRows => stateRows.Min(row => recencyRankByMeetingId[row.JednaniId]))
                    .First().Key);
    }
}
