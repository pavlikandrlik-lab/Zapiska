using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.People;

public sealed partial class PeopleService
{
    public async Task<OsobyIndexViewModel> BuildOsobyAsync(CancellationToken ct = default)
    {
        var organizationRows = await dbContext.CiselnikOrganizace
            .AsNoTracking()
            .ToListAsync(ct);
        var organizations = organizationRows.ToDictionary(x => x.Id);

        var orgUnitRows = await dbContext.CiselnikOrganizacniCelky
            .AsNoTracking()
            .ToListAsync(ct);
        var orgUnits = orgUnitRows.ToDictionary(x => x.Id);

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
        var peopleRows = await dbContext.Osoby
            .AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToListAsync(ct);

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
