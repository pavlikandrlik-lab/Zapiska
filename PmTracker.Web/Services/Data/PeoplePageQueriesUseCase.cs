using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class PeoplePageQueriesUseCase(PmTrackerDbContext dbContext) : IPeoplePageQueriesUseCase
{
    public OsobyIndexViewModel BuildOsoby()
    {
        var organizations = dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var organizationOptions = organizations.Values
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var orgUnitOptions = orgUnits.Values
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var peopleRows = dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToList();

        var people = peopleRows
            .Select(x => new OsobaListItemViewModel
            {
                Id = x.Id,
                Jmeno = x.Jmeno,
                Prijmeni = x.Prijmeni,
                Titul = x.Titul,
                Email = x.Email?.Trim() ?? string.Empty,
                OrganizaceKod = organizations.GetValueOrDefault(x.OrganizaceId)?.Kod,
                Organizace = organizations.GetValueOrDefault(x.OrganizaceId)?.Nazev,
                OrganizacniCelekKod = x.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(x.OrganizacniCelekId.Value)?.Kod : null,
                OrganizacniCelek = x.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(x.OrganizacniCelekId.Value)?.Nazev : null,
                JeAdUcet = x.GuidAd.HasValue,
                LocationLocked = x.LocationLocked
            })
            .ToList();

        return new OsobyIndexViewModel
        {
            Osoby = people,
            Organizace = organizationOptions,
            OrganizacniCelky = orgUnitOptions
        };
    }
}
