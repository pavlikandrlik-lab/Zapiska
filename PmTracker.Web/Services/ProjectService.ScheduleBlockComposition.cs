using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private static HarmonogramBlockViewModel BuildScheduleBlockViewModel(
        int recordId,
        string mode,
        DateTime datumZalozeni,
        DateTime terminUkonceni,
        string delayBarvaHex,
        HarmonogramSouhrnViewModel souhrn,
        IReadOnlyList<HarmonogramKrokEditViewModel> kroky,
        ScheduleEditorPermissionSet? permissions = null,
        string scheduleVersion = "",
        IReadOnlySet<int>? lockedManualKrokKeys = null,
        bool canEditManualActual = false)
    {
        var effectivePermissions = permissions ?? ScheduleEditorPermissionSet.ForReadOnly();
        return new HarmonogramBlockViewModel
        {
            RecordId = recordId,
            Mode = mode,
            DatumZalozeni = datumZalozeni.Date,
            TerminUkonceni = terminUkonceni.Date,
            DelayBarvaHex = delayBarvaHex,
            Souhrn = souhrn,
            Kroky = kroky,
            Permissions = effectivePermissions,
            EditorChangedTypeTooltips = new Dictionary<int, string>(),
            ScheduleVersion = scheduleVersion,
            LockedManualKrokKeys = lockedManualKrokKeys ?? new HashSet<int>(),
            CanEditManualActual = canEditManualActual
        };
    }
}
