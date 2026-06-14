using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Dictionaries;

public sealed partial class DictionaryService
{
    public async Task<CiselnikyDashboardViewModel> BuildCiselnikyDashboardAsync(
        string? id,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var items = await BuildCiselnikItemsAsync(ct);
        var requestedKey = string.IsNullOrWhiteSpace(id)
            ? null
            : id.Trim();
        var selected = items.Any(item => string.Equals(item.Key, requestedKey, StringComparison.OrdinalIgnoreCase))
            ? requestedKey!
            : items.FirstOrDefault()?.Key ?? "organizace";

        return new CiselnikyDashboardViewModel
        {
            Ciselniky = items,
            VybranyCiselnik = await BuildCiselnikDetailAsync(selected, currentUser, ct)
        };
    }

    public async Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(
        string id,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
    {
        var key = (id ?? string.Empty).Trim().ToLowerInvariant();
        var canChangeLockState = currentUser.IsSuperAdmin;

        return key switch
        {
            "stavy-projektu" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Stavy projektů",
                dbContext.CiselnikStavuProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "stavy-ukolu" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Stavy úkolů",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = ["Finální"],
                Polozky = await dbContext.CiselnikStavuUkolu
                    .AsNoTracking()
                    .OrderBy(x => x.Nazev)
                    .Select(x => new CiselnikRadekViewModel
                    {
                        Id = x.Id,
                        Kod = x.Kod,
                        Nazev = x.Nazev,
                        IsLocked = x.IsLocked,
                        CanChangeLockState = canChangeLockState,
                        HodnotyNavic = new[] { x.IsFinal ? "Ano" : "Ne" }
                    })
                    .ToListAsync(ct)
            },
            "kategorie-zaznamu" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Kategorie záznamů",
                dbContext.CiselnikKategoriiZaznamu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "typy-ukolu" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Typy úkolů",
                dbContext.CiselnikTypuUkolu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "typy-externich-odkazu" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Typy externích odkazů",
                dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "role-projektu" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Role projektu",
                dbContext.CiselnikRoliProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "role-subsystemu" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Role subsystému",
                dbContext.CiselnikRoliSubsystemu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "stavy-ucasti" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Stavy účasti",
                dbContext.CiselnikStavuUcasti.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "organizace" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Organizace",
                dbContext.CiselnikOrganizace.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "organizacni-celky" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Organizační celky",
                dbContext.CiselnikOrganizacniCelky.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            "subsystemy" => await BuildSubsystemyCiselnikDetailAsync(key, ct),
            "vyzvy" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Výzvy",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = ["Rok"],
                Polozky = await dbContext.Vyzvy
                    .AsNoTracking()
                    .OrderBy(x => x.Kod)
                    .Select(x => new CiselnikRadekViewModel
                    {
                        Id = x.Id,
                        Kod = x.Kod,
                        Nazev = "Výzva " + x.Kod,
                        IsLocked = x.Stav == VyzvaStav.Odeslano,
                        CanChangeLockState = canChangeLockState,
                        HodnotyNavic = new[] { x.Rok.ToString(CultureInfo.InvariantCulture) }
                    })
                    .ToListAsync(ct)
            },
            "stavy-jednani" => await BuildSimpleCiselnikDetailAsync(
                key,
                "Stavy jednání",
                dbContext.CiselnikStavuJednani.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct),
            _ => await BuildSimpleCiselnikDetailAsync(
                "stavy-projektu",
                "Stavy projektů",
                dbContext.CiselnikStavuProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }),
                canChangeLockState,
                ct)
        };
    }

    private static async Task<CiselnikDetailViewModel> BuildSimpleCiselnikDetailAsync(
        string key,
        string name,
        IQueryable<CiselnikRadekViewModel> rows,
        bool canChangeLockState,
        CancellationToken ct)
    {
        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = name,
            CanChangeLockState = canChangeLockState,
            SloupceNavic = Array.Empty<string>(),
            Polozky = await rows.OrderBy(x => x.Nazev).ToListAsync(ct)
        };
    }

    private async Task<CiselnikDetailViewModel> BuildSubsystemyCiselnikDetailAsync(string key, CancellationToken ct)
    {
        var subsystemRows = await dbContext.Subsystemy
            .AsNoTracking()
            .OrderBy(x => x.Nazev)
            .ToListAsync(ct);

        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = "Subsystémy",
            CanChangeLockState = false,
            SloupceNavic = Array.Empty<string>(),
            Polozky = subsystemRows
                .Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = false,
                    CanChangeLockState = false,
                    HodnotyNavic = Array.Empty<string>()
                })
                .ToList()
        };
    }

    private async Task<List<CiselnikListItemViewModel>> BuildCiselnikItemsAsync(CancellationToken ct)
    {
        return new List<CiselnikListItemViewModel>
        {
            new() { Key = "typy-externich-odkazu", Nazev = "Typy externích odkazů", PocetPolozek = await dbContext.CiselnikTypuExternichOdkazu.CountAsync(ct) },
            new() { Key = "role-projektu", Nazev = "Role projektu", PocetPolozek = await dbContext.CiselnikRoliProjektu.CountAsync(ct) },
            new() { Key = "role-subsystemu", Nazev = "Role subsystému", PocetPolozek = await dbContext.CiselnikRoliSubsystemu.CountAsync(ct) },
            new() { Key = "organizace", Nazev = "Organizace", PocetPolozek = await dbContext.CiselnikOrganizace.CountAsync(ct) },
            new() { Key = "organizacni-celky", Nazev = "Organizační celky", PocetPolozek = await dbContext.CiselnikOrganizacniCelky.CountAsync(ct) },
            new() { Key = "subsystemy", Nazev = "Subsystémy", PocetPolozek = await dbContext.Subsystemy.CountAsync(ct) },
            new() { Key = "vyzvy", Nazev = "Výzvy", PocetPolozek = await dbContext.Vyzvy.CountAsync(ct) }
        };
    }
}
