using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dictionaries;

namespace PmTracker.Tests.Unit.Dictionaries;

public sealed class DictionariesServiceTests
{
    [Fact]
    public void BuildCiselnikyDashboard_ShouldDelegateToDataStore()
    {
        var expected = new CiselnikyDashboardViewModel
        {
            Ciselniky = [],
            VybranyCiselnik = new CiselnikDetailViewModel
            {
                Key = "test",
                Nazev = "Test",
                SloupceNavic = [],
                Polozky = []
            }
        };
        var dataStore = new FakeDictionariesDataStore(expected);
        var sut = new DictionariesService(dataStore);
        var user = BuildUser();

        var result = sut.BuildCiselnikyDashboard("test", user);

        result.Should().BeSameAs(expected);
        dataStore.LastDashboardUser.Should().BeSameAs(user);
    }

    private static CurrentUserContextViewModel BuildUser()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 17,
            Jmeno = "Unit",
            Prijmeni = "Tester",
            DisplayName = "Unit Tester",
            Email = "unit@test.local",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
            IsSuperAdmin = true,
            RoleKody = [],
            VisibleProjectIds = [],
            DeletedProjectIds = [],
            PermissionGrants = []
        };
    }

    private sealed class FakeDictionariesDataStore(CiselnikyDashboardViewModel dashboard) : IDictionariesDataStore
    {
        public CurrentUserContextViewModel? LastDashboardUser { get; private set; }

        public CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser)
        {
            LastDashboardUser = currentUser;
            return dashboard;
        }

        public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser) => dashboard.VybranyCiselnik;

        public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
        {
        }

        public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }
}
