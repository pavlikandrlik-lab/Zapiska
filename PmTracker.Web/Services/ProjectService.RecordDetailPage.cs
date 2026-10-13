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
    public async Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)
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
                // Odkaz na zdrojový tiket vidí každý, kdo vidí záznam — okno jen pro čtení
                // (2026-10-13). Výběr vazby = stejné pravidlo jako editor (ResolveMatched).
                VyjadreniOknoUpravy = false,
                Kroky = await AttachSourceVyjadreniAsync(recordId, schedule.HarmonogramBlok.Kroky, ct)
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
    /// kandidáta, tady stačí uložená vazba). Výběr vazby = stejné pravidlo jako editor
    /// (<c>HarmonogramSkutecnostResolver.ResolveMatched</c>): krok 1 nejstarší, ostatní preferovaná, jinak nejnovější.
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

        var winnerByPoradi = vazby
            .GroupBy(v => v.Poradi)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var resolved = HarmonogramSkutecnostResolver.ResolveMatched(
                        g.Key,
                        g.Select(v => new BindingKandidat(v.ExterniOdkazId, "", "", "", v.DatumVyjadreni, v.HotVyjadreniId)),
                        preferredByPoradi.TryGetValue(g.Key, out var preferred) ? preferred : null);
                    return resolved.Kandidati.First(k => k.ExterniOdkazId == resolved.VybranyExterniOdkazId);
                });

        return kroky
            .Select(k =>
            {
                if (winnerByPoradi.TryGetValue((byte)k.KrokIndex, out var winner))
                {
                    k.SourceExterniOdkazId = winner.ExterniOdkazId;
                    k.SourceVyjadreniId = winner.HotVyjadreniId > 0 ? winner.HotVyjadreniId : null;
                    k.SourceVyjadreniDatum = winner.Datum;
                }
                return k;
            })
            .ToList();
    }
}
