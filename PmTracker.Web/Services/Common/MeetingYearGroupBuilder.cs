using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

internal static class MeetingYearGroupBuilder
{
    public static IReadOnlyList<JednaniYearGroupViewModel> BuildYearGroups(IEnumerable<JednaniListItemViewModel> meetings)
    {
        return meetings
            .GroupBy(meeting => meeting.Datum.Year)
            .OrderByDescending(group => group.Key)
            .Select(group => new JednaniYearGroupViewModel
            {
                Rok = group.Key,
                Jednani = group
                    .OrderByDescending(meeting => meeting.CisloJednani)
                    .ThenByDescending(meeting => meeting.Datum)
                    .ThenByDescending(meeting => meeting.CasZacatek)
                    .ToList()
            })
            .ToList();
    }

    public static int? ResolvePreviewYear(IReadOnlyList<JednaniYearGroupViewModel> yearGroups, int currentYear)
    {
        if (yearGroups.Count == 0)
        {
            return null;
        }

        if (yearGroups.Any(group => group.Rok == currentYear))
        {
            return currentYear;
        }

        return yearGroups
            .OrderByDescending(group => group.Rok)
            .Select(group => (int?)group.Rok)
            .FirstOrDefault();
    }
}
