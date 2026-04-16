using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService
{
    public async Task<MeetingModalViewModel> BuildNewMeetingModalAsync(int projectId, DateTime localNow, CancellationToken ct = default)
    {
        var existingMeetingNumbers = await LoadExistingMeetingNumbersAsync(projectId, ct);
        var statusOptions = await BuildMeetingStatusOptionsAsync(ct);
        var nextMeetingNumber = existingMeetingNumbers.Count == 0 ? 1 : existingMeetingNumbers[^1] + 1;
        var defaultStatus = statusOptions.FirstOrDefault()?.Value ?? string.Empty;

        return new MeetingModalViewModel
        {
            Title = "Nové jednání",
            Command = new SaveMeetingCommand
            {
                ProjektId = projectId,
                CisloJednani = nextMeetingNumber,
                DatumPlanovane = localNow.Date,
                CasZacatek = TimeOnly.FromDateTime(localNow),
                StavJednani = defaultStatus
            },
            ExistingMeetingNumbersCsv = string.Join(",", existingMeetingNumbers),
            StavyJednani = statusOptions
        };
    }

    public async Task<MeetingModalViewModel?> BuildEditMeetingModalAsync(int projectId, int meetingId, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == meetingId && x.ProjektId == projectId)
            .Select(x => new
            {
                x.Id,
                x.ProjektId,
                x.CisloJednani,
                x.DatumPlanovane,
                x.CasZacatek,
                x.Misto,
                x.StavJednaniId
            })
            .FirstOrDefaultAsync(ct);
        if (meeting is null)
        {
            return null;
        }

        var statusOptions = await BuildMeetingStatusOptionsAsync(ct);
        var existingMeetingNumbers = await LoadExistingMeetingNumbersAsync(projectId, ct);
        var statusCode = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .Where(x => x.Id == meeting.StavJednaniId)
            .Select(x => x.Kod)
            .FirstOrDefaultAsync(ct);

        return new MeetingModalViewModel
        {
            Title = "Upravit jednání",
            Command = new SaveMeetingCommand
            {
                Id = meeting.Id,
                ProjektId = meeting.ProjektId,
                CisloJednani = meeting.CisloJednani,
                DatumPlanovane = meeting.DatumPlanovane,
                CasZacatek = meeting.CasZacatek,
                Misto = meeting.Misto,
                StavJednani = statusCode ?? statusOptions.FirstOrDefault()?.Value ?? string.Empty
            },
            ExistingMeetingNumbersCsv = string.Join(",", existingMeetingNumbers),
            StavyJednani = statusOptions
        };
    }

    public async Task<bool?> IsMeetingEditableAsync(int projectId, int meetingId, CancellationToken ct = default)
    {
        var meetingState = await (
                from meeting in dbContext.Jednani.AsNoTracking()
                join state in dbContext.CiselnikStavuJednani.AsNoTracking() on meeting.StavJednaniId equals state.Id
                where meeting.Id == meetingId && meeting.ProjektId == projectId
                select new
                {
                    Meeting = meeting,
                    State = state
                })
            .FirstOrDefaultAsync(ct);

        if (meetingState is null)
        {
            return null;
        }

        return !IsMeetingReadOnly(meetingState.Meeting, meetingState.State);
    }

    private Task<List<int>> LoadExistingMeetingNumbersAsync(int projectId, CancellationToken ct)
        => dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => x.CisloJednani)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);

    private Task<List<LookupOptionViewModel>> BuildMeetingStatusOptionsAsync(CancellationToken ct)
        => dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
}
