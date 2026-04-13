using PmTracker.Web.Models.ViewModels;

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
        bool editorJeUkolKategorie = false,
        bool editorCanEditScheduleFull = false,
        bool editorCanEditScheduleAddOnly = false)
    {
        return new HarmonogramBlockViewModel
        {
            RecordId = recordId,
            Mode = mode,
            DatumZalozeni = datumZalozeni.Date,
            TerminUkonceni = terminUkonceni.Date,
            DelayBarvaHex = delayBarvaHex,
            Souhrn = souhrn,
            Kroky = kroky,
            EditorJeUkolKategorie = editorJeUkolKategorie,
            EditorCanEditScheduleFull = editorCanEditScheduleFull,
            EditorCanEditScheduleAddOnly = editorCanEditScheduleAddOnly
        };
    }
}
