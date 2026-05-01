using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    /// <summary>
    /// DRY helper pro <c>ProjectFilterShellViewModel</c> — single source of truth pro 10
    /// lookup options sdílených mezi Records a Schedule taby. Spec
    /// 2026-04-30-project-filter-unification-design (2026-04-30).
    /// Owner list je union všech aktivních vlastníků záznamů projektu, aby filter state
    /// byl validní na obou tabech (záznam, který filtruje "vlastník=X" v Records, musí mít
    /// option v Schedule tabu i když daný owner aktuálně nemá žádný harmonogramový úkol).
    /// </summary>
    private async Task<ProjectFilterShellViewModel> BuildProjectFilterShellAsync(
        int projektId,
        string scope,
        CancellationToken ct)
    {
        var activeProjectSubsystems = await BuildActiveProjectSubsystemsAsync(projektId, ct);
        var subsystemOptions = activeProjectSubsystems
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();

        // Čtyři číselníky z cached lookup tablek (stejný pattern jako BuildProjectRecordsTabAsync).
        var categoryOptions = (await lookupCache.GetCategoriesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();
        var taskStateOptions = (await lookupCache.GetTaskStatesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();
        var taskTypeOptions = (await lookupCache.GetTaskTypesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();
        var meetingStatusOptions = (await lookupCache.GetMeetingStatesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Label = x.Nazev
            })
            .ToList();

        // Owners — union všech aktuálních vlastníků aktivních záznamů projektu (distinct VlastnikId
        // přes ProjektoveZaznamy JOIN Osoby). Sjednocený zdroj pro records i schedule scope, aby
        // filter state byl validní na obou tabech (vlastník zvolený v Records musí mít odpovídající
        // option v Schedule selectu, jinak by byl filter "neznámý vlastník").
        var ownerOptions = await (
                from z in dbContext.ProjektoveZaznamy.AsNoTracking()
                where z.ProjektId == projektId
                join o in dbContext.Osoby.AsNoTracking() on z.VlastnikId equals o.Id
                select new { o.Id, o.Jmeno, o.Prijmeni })
            .Distinct()
            .ToListAsync(ct);
        var ownerLookups = ownerOptions
            .Select(o => new LookupOptionViewModel
            {
                Value = o.Id.ToString(CultureInfo.InvariantCulture),
                Label = $"{o.Jmeno} {o.Prijmeni}".Trim()
            })
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ProjectFilterShellViewModel
        {
            ProjektId = projektId,
            Scope = scope,
            SubsystemyMoznosti = subsystemOptions,
            KategorieMoznosti = categoryOptions,
            StavyUkoluMoznosti = taskStateOptions,
            TypyUkoluMoznosti = taskTypeOptions,
            VlastniciMoznosti = ownerLookups,
            StavyJednaniVyjadreni = meetingStatusOptions
        };
    }
}
