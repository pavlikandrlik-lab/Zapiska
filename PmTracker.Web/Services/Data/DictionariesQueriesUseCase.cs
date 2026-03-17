using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class DictionariesQueriesUseCase(PmTrackerDbContext dbContext) : IDictionariesQueriesUseCase
{
    private const string HarmonogramKrokyCiselnikKey = "harmonogram-kroky";

    public CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser, IDictionariesQueriesComposition composition)
    {
        var items = BuildCiselnikItems(composition);
        var selected = string.IsNullOrWhiteSpace(id)
            ? items.FirstOrDefault()?.Key ?? "stavy-projektu"
            : id.Trim();

        return new CiselnikyDashboardViewModel
        {
            Ciselniky = items,
            VybranyCiselnik = BuildCiselnikDetail(selected, currentUser, composition)
        };
    }

    public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser, IDictionariesQueriesComposition composition)
    {
        var key = (id ?? string.Empty).Trim().ToLowerInvariant();
        var canChangeLockState = currentUser.IsSuperAdmin;

        return key switch
        {
            "stavy-projektu" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Stavy projektů",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = Array.Empty<string>(),
                Polozky = dbContext.CiselnikStavuProjektu.AsNoTracking().OrderBy(x => x.Nazev).Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = Array.Empty<string>()
                }).ToList()
            },
            "stavy-ukolu" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Stavy úkolů",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = new[] { "Finální" },
                Polozky = dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = new[] { x.IsFinal ? "Ano" : "Ne" }
                }).ToList()
            },
            "kategorie-zaznamu" => BuildSimpleCiselnikDetail(key, "Kategorie záznamů", dbContext.CiselnikKategoriiZaznamu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "typy-ukolu" => BuildSimpleCiselnikDetail(key, "Typy úkolů", dbContext.CiselnikTypuUkolu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "typy-externich-odkazu" => BuildSimpleCiselnikDetail(key, "Typy externích odkazů", dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "role-projektu" => BuildSimpleCiselnikDetail(key, "Role projektu", dbContext.CiselnikRoliProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "role-subsystemu" => BuildSimpleCiselnikDetail(key, "Role subsystému", dbContext.CiselnikRoliSubsystemu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "stavy-ucasti" => BuildSimpleCiselnikDetail(key, "Stavy účasti", dbContext.CiselnikStavuUcasti.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "organizace" => BuildSimpleCiselnikDetail(key, "Organizace", dbContext.CiselnikOrganizace.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "organizacni-celky" => BuildSimpleCiselnikDetail(key, "Organizační celky", dbContext.CiselnikOrganizacniCelky.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            "subsystemy" => BuildSubsystemyCiselnikDetail(key),
            HarmonogramKrokyCiselnikKey => composition.BuildHarmonogramKrokyCiselnikDetail(key, canChangeLockState),
            "vyzvy" => new CiselnikDetailViewModel
            {
                Key = key,
                Nazev = "Výzvy",
                CanChangeLockState = canChangeLockState,
                SloupceNavic = new[] { "Rok" },
                Polozky = dbContext.CiselnikVyzvy.AsNoTracking().OrderBy(x => x.Kod).Select(x => new CiselnikRadekViewModel
                {
                    Id = x.Id,
                    Kod = x.Kod,
                    Nazev = x.Nazev,
                    IsLocked = x.IsLocked,
                    CanChangeLockState = canChangeLockState,
                    HodnotyNavic = new[] { x.Rok.ToString("yyyy", CultureInfo.InvariantCulture) }
                }).ToList()
            },
            "stavy-jednani" => BuildSimpleCiselnikDetail(key, "Stavy jednání", dbContext.CiselnikStavuJednani.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState),
            _ => BuildSimpleCiselnikDetail("stavy-projektu", "Stavy projektů", dbContext.CiselnikStavuProjektu.AsNoTracking().Select(x => new CiselnikRadekViewModel
            {
                Id = x.Id,
                Kod = x.Kod,
                Nazev = x.Nazev,
                IsLocked = x.IsLocked,
                CanChangeLockState = canChangeLockState,
                HodnotyNavic = Array.Empty<string>()
            }), canChangeLockState)
        };
    }

    private CiselnikDetailViewModel BuildSimpleCiselnikDetail(string key, string name, IQueryable<CiselnikRadekViewModel> rows, bool canChangeLockState)
    {
        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = name,
            CanChangeLockState = canChangeLockState,
            SloupceNavic = Array.Empty<string>(),
            Polozky = rows.OrderBy(x => x.Nazev).ToList()
        };
    }

    private CiselnikDetailViewModel BuildSubsystemyCiselnikDetail(string key)
    {
        var subsystemRows = dbContext.Subsystemy.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .ToList();

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

    private List<CiselnikListItemViewModel> BuildCiselnikItems(IDictionariesQueriesComposition composition)
    {
        return new List<CiselnikListItemViewModel>
        {
            new() { Key = "stavy-projektu", Nazev = "Stavy projektů", PocetPolozek = dbContext.CiselnikStavuProjektu.Count() },
            new() { Key = "stavy-ukolu", Nazev = "Stavy úkolů", PocetPolozek = dbContext.CiselnikStavuUkolu.Count() },
            new() { Key = "kategorie-zaznamu", Nazev = "Kategorie záznamů", PocetPolozek = dbContext.CiselnikKategoriiZaznamu.Count() },
            new() { Key = "typy-ukolu", Nazev = "Typy úkolů", PocetPolozek = dbContext.CiselnikTypuUkolu.Count() },
            new() { Key = "typy-externich-odkazu", Nazev = "Typy externích odkazů", PocetPolozek = dbContext.CiselnikTypuExternichOdkazu.Count() },
            new() { Key = "role-projektu", Nazev = "Role projektu", PocetPolozek = dbContext.CiselnikRoliProjektu.Count() },
            new() { Key = "role-subsystemu", Nazev = "Role subsystému", PocetPolozek = dbContext.CiselnikRoliSubsystemu.Count() },
            new() { Key = "stavy-ucasti", Nazev = "Stavy účasti", PocetPolozek = dbContext.CiselnikStavuUcasti.Count() },
            new() { Key = "organizace", Nazev = "Organizace", PocetPolozek = dbContext.CiselnikOrganizace.Count() },
            new() { Key = "organizacni-celky", Nazev = "Organizační celky", PocetPolozek = dbContext.CiselnikOrganizacniCelky.Count() },
            new() { Key = "subsystemy", Nazev = "Subsystémy", PocetPolozek = dbContext.Subsystemy.Count() },
            new() { Key = HarmonogramKrokyCiselnikKey, Nazev = "Harmonogramové kroky", PocetPolozek = composition.CountHarmonogramCatalogRows() },
            new() { Key = "vyzvy", Nazev = "Výzvy", PocetPolozek = dbContext.CiselnikVyzvy.Count() },
            new() { Key = "stavy-jednani", Nazev = "Stavy jednání", PocetPolozek = dbContext.CiselnikStavuJednani.Count() }
        };
    }
}
