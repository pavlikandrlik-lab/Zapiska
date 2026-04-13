using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Home;

public sealed class HomeDashboardService : IHomeDashboardService
{
    public DashboardPageViewModel BuildDashboard(CurrentUserContextViewModel currentUser)
    {
        var availableModules = CountAvailableModules(currentUser);
        var quickAccessCards = BuildQuickAccessCards(currentUser);

        return new DashboardPageViewModel
        {
            PageTitle = "Přehled",
            Subtitle = "Výchozí rozcestník pro každodenní práci v aplikaci.",
            Hero = new DashboardHeroViewModel
            {
                Eyebrow = "Uživatelský dashboard",
                Title = $"Dobrý den, {ResolveGreetingName(currentUser)}",
                Description = "Tato stránka je připravena jako hlavní rozcestník. Sekce i dlaždice jsou rozdělené tak, aby se daly postupně doplňovat bez dalšího přestavování shellu.",
                Stats =
                [
                    new DashboardStatViewModel
                    {
                        Label = "Dostupné moduly",
                        Value = availableModules.ToString()
                    },
                    new DashboardStatViewModel
                    {
                        Label = "Aktivní role",
                        Value = currentUser.RoleKody.Count.ToString()
                    },
                    new DashboardStatViewModel
                    {
                        Label = "Viditelné projekty",
                        Value = currentUser.IsSuperAdmin ? "vše" : currentUser.VisibleProjectIds.Count.ToString()
                    }
                ]
            },
            Sections =
            [
                new DashboardSectionViewModel
                {
                    Key = "quick-access",
                    Title = "Rychlé vstupy",
                    Description = "Základní navigační kostra pro nejčastější moduly a osobní agendu.",
                    Cards = quickAccessCards
                },
                new DashboardSectionViewModel
                {
                    Key = "workspace",
                    Title = "Moje práce",
                    Description = "Rezervovaný blok pro osobní fronty, návrhy, úkoly a rozpracované položky.",
                    Cards =
                    [
                        CreatePlaceholderCard("Moje úkoly", "Sem se později doplní osobní pracovní fronta uživatele."),
                        CreatePlaceholderCard("Čekající návrhy", "Připraveno pro přehled pending návrhů a rozhodnutí."),
                        CreatePlaceholderCard("Rozpracovaná jednání", "Rezervované místo pro blok práce navázané na jednání.")
                    ]
                },
                new DashboardSectionViewModel
                {
                    Key = "overview",
                    Title = "Přehledy a hlídání",
                    Description = "Samostatný prostor pro upozornění, termíny, rizika a klíčové souhrny.",
                    Cards =
                    [
                        CreatePlaceholderCard("Termíny a rizika", "Sem lze doplnit deadline, eskalace a kritické změny."),
                        CreatePlaceholderCard("Stav projektů", "Připraveno pro manažerský nebo osobní souhrn stavu projektů."),
                        CreatePlaceholderCard("Důležité změny", "Rezervováno pro novinky, notifikace a systémové informace.")
                    ]
                }
            ]
        };
    }

    private static string ResolveGreetingName(CurrentUserContextViewModel currentUser)
    {
        return string.IsNullOrWhiteSpace(currentUser.Jmeno)
            ? currentUser.DisplayName
            : currentUser.Jmeno;
    }

    private static int CountAvailableModules(CurrentUserContextViewModel currentUser)
    {
        var count = 3;
        if (currentUser.HasPermissionPrefix(PermissionKeys.PeoplePrefix))
        {
            count++;
        }

        if (currentUser.HasPermissionPrefix(PermissionKeys.CiselnikyPrefix))
        {
            count++;
        }

        if (currentUser.HasPermissionPrefix(PermissionKeys.SettingsPrefix))
        {
            count++;
        }

        return count;
    }

    private static IReadOnlyList<DashboardCardViewModel> BuildQuickAccessCards(CurrentUserContextViewModel currentUser)
    {
        var cards = new List<DashboardCardViewModel>
        {
            new()
            {
                Title = "Projekty",
                Description = "Vstup do projektů, detailů a pracovních flow nad záznamy.",
                Badge = "Hlavní modul",
                Meta = "Výchozí pracovní oblast",
                Controller = "Projekty",
                Action = "Index",
                IsPrimary = true
            },
            new()
            {
                Title = "Jednání",
                Description = "Přehled jednání a návazné práce se zápisem a úkoly.",
                Controller = "Jednani",
                Action = "Index"
            },
            new()
            {
                Title = "Můj profil",
                Description = "Osobní přehled rolí, práv a základních údajů uživatele.",
                Controller = "Profil",
                Action = "Index"
            }
        };

        if (currentUser.HasPermissionPrefix(PermissionKeys.PeoplePrefix))
        {
            cards.Add(new DashboardCardViewModel
            {
                Title = "Osoby",
                Description = "Správa a vyhledávání osob dostupných přihlášenému uživateli.",
                Controller = "Osoby",
                Action = "Index"
            });
        }

        if (currentUser.HasPermissionPrefix(PermissionKeys.CiselnikyPrefix))
        {
            cards.Add(new DashboardCardViewModel
            {
                Title = "Číselníky",
                Description = "Referenční data a administrace katalogů používaných aplikací.",
                Controller = "Ciselniky",
                Action = "Index"
            });
        }

        if (currentUser.HasPermissionPrefix(PermissionKeys.SettingsPrefix))
        {
            cards.Add(new DashboardCardViewModel
            {
                Title = "Nastavení",
                Description = "Role, oprávnění a bezpečnostní konfigurace aplikace.",
                Controller = "Nastaveni",
                Action = "Index"
            });
        }

        return cards;
    }

    private static DashboardCardViewModel CreatePlaceholderCard(string title, string description)
    {
        return new DashboardCardViewModel
        {
            Title = title,
            Description = description,
            Badge = "Připraveno pro doplnění",
            IsPlaceholder = true
        };
    }
}
