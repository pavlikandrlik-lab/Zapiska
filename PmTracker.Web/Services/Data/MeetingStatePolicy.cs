using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Data;

internal static class MeetingStatePolicy
{
    public static bool IsReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
    {
        if (meeting is null)
        {
            return true;
        }

        if (meeting.UzamklOsobaId.HasValue)
        {
            return true;
        }

        if (status is null)
        {
            return false;
        }

        return string.Equals(status.Kod, "CLOSED", StringComparison.OrdinalIgnoreCase)
            || status.Nazev.Contains("uzav", StringComparison.OrdinalIgnoreCase);
    }
}
