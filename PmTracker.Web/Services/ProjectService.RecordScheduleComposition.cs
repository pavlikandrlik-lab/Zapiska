using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    /// <summary>
    /// Toggle na kartě záznamu (2026-07-13): harmonogram JEDNOHO záznamu pro pohled
    /// „harmonogram" na kartě v záložce Záznamy. Stejné stavební kameny jako
    /// BuildProjectScheduleRowsAsync (záložka Harmonogram) — jen pro jeden záznam,
    /// s BreakdownExpanded (rozpad rovnou viditelný). NULL = záznam neexistuje /
    /// není úkol / nemá vyplněnou hodnotu kroku (stejný predikát jako dlaždice v záložce).
    /// </summary>
    public async Task<ZaznamScheduleBlockViewModel?> BuildRecordScheduleBlockAsync(int projectId, int recordId, CancellationToken ct = default)
    {
        var shell = await BuildRecordCardShellAsync(projectId, recordId, ct);
        if (shell is null || !shell.Summary.JeUkol)
        {
            return null;
        }

        var kroky = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToListAsync(ct);
        if (!HarmonogramKrokPredicates.MaVyplnenouHodnotu(kroky))
        {
            return null;
        }

        var summary = shell.Summary;
        var todayDate = timeProvider.GetLocalNow().Date;
        var deadline = (summary.AktualniTermin ?? summary.DatumZalozeni).Date;
        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(summary.DatumZalozeni, kroky, deadline, todayDate);

        return new ZaznamScheduleBlockViewModel
        {
            Stihame = souhrn.Stihame,
            HarmonogramBlok = BuildScheduleBlockViewModel(
                recordId,
                "project-readonly",
                summary.DatumZalozeni,
                deadline,
                "#dc2626",
                souhrn,
                HarmonogramDateBlokBuilder.BuildKroky(summary.DatumZalozeni, kroky, todayDate),
                overviewLayout: HarmonogramDateBlokBuilder.BuildBarLayout(summary.DatumZalozeni, kroky, deadline, todayDate),
                today: todayDate) with { BreakdownExpanded = true }
        };
    }
}
