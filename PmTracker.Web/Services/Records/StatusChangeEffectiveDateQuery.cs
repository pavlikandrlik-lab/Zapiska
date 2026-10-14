using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

public sealed record StatusChangeEffectiveDateContext(
    IReadOnlyList<StatusChangeMeetingCandidate> Meetings,
    StatusChangeEffectiveDateChoice Choice);

/// <summary>
/// Nabídka „Změna stavu platí k“ nad daty projektu a záznamu (spec 2026-10-13 §4.2–§4.4).
/// Stejný výpočet používá editor pro zobrazení i uložení pro ověření, aby se nemohly rozejít.
/// </summary>
public static class StatusChangeEffectiveDateQuery
{
    public static async Task<StatusChangeEffectiveDateContext> LoadAsync(
        PmTrackerDbContext dbContext,
        int projektId,
        int zaznamId,
        DateTime recordFoundedOn,
        DateTime today,
        CancellationToken ct)
    {
        var meetings = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .ToListAsync(ct);
        var meetingStates = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);
        var candidates = meetings
            .Select(meeting =>
            {
                var state = meetingStates.GetValueOrDefault(meeting.StavJednaniId);
                return new StatusChangeMeetingCandidate(
                    meeting.Id,
                    meeting.CisloJednani,
                    meeting.DatumPlanovane.Date,
                    state?.Kod,
                    MeetingStatePolicy.IsReadOnly(meeting, state));
            })
            .ToList();
        var lastEffectiveDate = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId)
            .MaxAsync(x => (DateTime?)x.PlatiOd, ct);

        return new StatusChangeEffectiveDateContext(
            candidates,
            StatusChangeEffectiveDatePolicy.Build(candidates, today, lastEffectiveDate, recordFoundedOn));
    }
}
