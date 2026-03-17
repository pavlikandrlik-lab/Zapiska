using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

public sealed class MeetingListQueriesUseCase(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer,
    IPersonIdentityMatcher personIdentityMatcher) : IMeetingListQueriesUseCase
{
    public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview()
    {
        var projects = dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new { x.Id, x.CelyNazev })
            .ToList();

        return projects
            .Select(project => new JednaniProjektListItemViewModel
            {
                ProjektId = project.Id,
                ProjektNazev = project.CelyNazev,
                Jednani = BuildJednaniList(project.Id)
            })
            .Where(x => x.Jednani.Count > 0)
            .ToList();
    }

    public IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projektId)
    {
        var meetings = dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .ToList();

        var statusById = dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);
        var personsById = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return meetings
            .OrderByDescending(x => x.CisloJednani)
            .ThenByDescending(x => x.DatumPlanovane)
            .ThenByDescending(x => x.CasZacatek)
            .Select(x => new JednaniListItemViewModel
            {
                Id = x.Id,
                CisloJednani = x.CisloJednani,
                Datum = x.DatumPlanovane,
                CasZacatek = x.CasZacatek,
                Misto = string.IsNullOrWhiteSpace(x.Misto) ? "-" : x.Misto,
                StavKod = statusById.GetValueOrDefault(x.StavJednaniId)?.Kod,
                Stav = statusById.GetValueOrDefault(x.StavJednaniId)?.Nazev ?? "-",
                UzamklOsoba = x.UzamklOsobaId.HasValue ? BuildInlinePersonLabelFromOsoba(personsById.GetValueOrDefault(x.UzamklOsobaId.Value)) : null
            })
            .ToList();
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }

    private string BuildInlinePersonLabel(string? titul, string jmeno, string prijmeni, string? email, int id)
    {
        var displayName = BuildDisplayName(titul, jmeno, prijmeni, id);
        var normalizedEmail = textNormalizer.NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? displayName
            : $"{displayName} <{normalizedEmail}>";
    }

    private string BuildInlinePersonLabelFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildInlinePersonLabel(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Email, osoba.Id);
    }
}
