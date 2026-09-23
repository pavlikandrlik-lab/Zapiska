using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    /// <summary>
    /// Stránka záznamu (2026-07-14): složí data pro read-only stránku jednoho záznamu.
    /// Reuse existujících builderů — žádná nová dotazovací logika. Tabulková podoba je
    /// tentýž blok jako grafická, jen v editor módu se zamčenými oprávněními (render
    /// datumů jako text). NULL = záznam neexistuje nebo na něj uživatel nemá přístup.
    /// Oprávnění a URL doplňuje controller (presentation vrstva).
    /// </summary>
    public Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)
        => BuildRecordDetailPageAsync(projectId, recordId, canOpenVyjadreni: false, ct);

    /// <param name="canOpenVyjadreni">
    /// Smí uživatel otevřít okno vyjádření (<c>vyjadreni.modal.open</c>)? Jen pak se u vytěžené
    /// skutečnosti nabídne odkaz „odkud pochází" — jinak by vedl na akci, kterou server odmítne.
    /// </param>
    public async Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, bool canOpenVyjadreni, CancellationToken ct = default)
    {
        var shell = await BuildRecordCardShellAsync(projectId, recordId, ct);
        if (shell is null)
        {
            return null;
        }

        var detail = await BuildRecordCardDetailAsync(projectId, recordId, ct);
        var comments = await BuildRecordCommentsPanelAsync(projectId, recordId, ct);
        if (detail is null || comments is null)
        {
            return null;
        }

        var schedule = await BuildRecordScheduleBlockAsync(projectId, recordId, ct);
        // Tabulková podoba: stejný blok v editor módu, ale plně zamčený. ForReadOnly() má
        // IsTaskCategory = false, což v tabulce zamkne plánová i skutečnostní datumová pole
        // (_AppDateField je pak renderuje jako text, ne input).
        var scheduleTable = schedule is null
            ? null
            : schedule.HarmonogramBlok with
            {
                Mode = "record-editor",
                Permissions = ScheduleEditorPermissionSet.ForReadOnly(),
                CanEditManualActual = false,
                Kroky = canOpenVyjadreni
                    ? await AttachSourceVyjadreniAsync(recordId, schedule.HarmonogramBlok.Kroky, ct)
                    : schedule.HarmonogramBlok.Kroky
            };

        // Jen zkratka + název pro drobečky — BuildProjektDetailAsync by načetl celou
        // záložku Záznamy (těžký dotaz kvůli dvěma řetězcům).
        var projekt = await dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new { x.Zkratka, x.CelyNazev })
            .FirstOrDefaultAsync(ct);
        if (projekt is null)
        {
            return null;
        }

        return new ZaznamDetailPageViewModel
        {
            ProjektId = projectId,
            ProjektZkratka = projekt.Zkratka,
            ProjektNazev = projekt.CelyNazev,
            Summary = shell.Summary,
            Detail = detail,
            Comments = comments,
            Schedule = schedule,
            ScheduleTable = scheduleTable
        };
    }

    /// <summary>
    /// Doplní ke krokům s vytěženou skutečností odkaz na zdrojové vyjádření (ikona „odkud to je").
    /// Bere aktivní vazbu se stejným pořadím — bez dotazu do ServiceDesku, takže zobrazení
    /// nezávisí na jeho dostupnosti (resolver v editoru fingerprinty potřebuje kvůli výběru
    /// kandidáta, tady stačí uložená vazba). Při více vazbách vyhrává preferovaná, jinak nejnovější.
    /// </summary>
    private async Task<IReadOnlyList<HarmonogramKrokEditViewModel>> AttachSourceVyjadreniAsync(
        int recordId,
        IReadOnlyList<HarmonogramKrokEditViewModel> kroky,
        CancellationToken ct)
    {
        var harvested = kroky
            .Where(k => k.ZdrojSkutecnosti == ZdrojSkutecnosti.FromVyjadreni)
            .Select(k => (byte)k.KrokIndex)
            .ToList();
        if (harvested.Count == 0)
        {
            return kroky;
        }

        var vazby = await dbContext.VyjadreniVazby.AsNoTracking()
            .Where(v => v.ZaznamId == recordId
                && v.Stav == (byte)VazbaStav.Active
                && harvested.Contains(v.Poradi))
            .ToListAsync(ct);
        if (vazby.Count == 0)
        {
            return kroky;
        }

        var preferredByPoradi = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId && x.PreferredExterniOdkazId != null)
            .ToDictionaryAsync(x => x.Poradi, x => x.PreferredExterniOdkazId!.Value, ct);

        var byPoradi = vazby
            .GroupBy(v => v.Poradi)
            .ToDictionary(
                g => g.Key,
                g => preferredByPoradi.TryGetValue(g.Key, out var preferred)
                        && g.FirstOrDefault(v => v.ExterniOdkazId == preferred) is { } match
                    ? match
                    : g.OrderByDescending(v => v.DatumVyjadreni).First());

        return kroky
            .Select(k =>
            {
                if (byPoradi.TryGetValue((byte)k.KrokIndex, out var vazba))
                {
                    k.SourceExterniOdkazId = vazba.ExterniOdkazId;
                    k.SourceVyjadreniId = vazba.HotVyjadreniId > 0 ? vazba.HotVyjadreniId : null;
                    k.SourceVyjadreniDatum = vazba.DatumVyjadreni;
                }
                return k;
            })
            .ToList();
    }
}
