using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Profile;

namespace PmTracker.Tests.Unit.Profile;

public sealed class ProfileServiceTests
{
    [Fact]
    public void BuildProfilPage_ShouldDelegateToDataStore()
    {
        var expected = new ProfilPageViewModel
        {
            Uzivatel = new CurrentUserContextViewModel
            {
                OsobaId = 17,
                Jmeno = "Unit",
                Prijmeni = "Tester",
                DisplayName = "Unit Tester",
                Email = "unit@test.local",
                OrganizacniCelek = "QA",
                OrganizacniCelekKod = "QA",
                IsSuperAdmin = false,
                RoleKody = [],
                VisibleProjectIds = [],
                DeletedProjectIds = [],
                PermissionGrants = []
            },
            MojeRole = [],
            OdvozenaPrava = []
        };
        var dataStore = new FakeProfileDataStore(expected);
        var sut = new ProfileService(dataStore);

        var result = sut.BuildProfilPage(expected.Uzivatel, projektId: 123);

        result.Should().BeSameAs(expected);
        dataStore.LastProjectId.Should().Be(123);
        dataStore.LastUser.Should().BeSameAs(expected.Uzivatel);
    }

    private sealed class FakeProfileDataStore(ProfilPageViewModel response) : IProfileDataStore
    {
        public CurrentUserContextViewModel? LastUser { get; private set; }
        public int? LastProjectId { get; private set; }

        public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
        {
            LastUser = currentUser;
            LastProjectId = projektId;
            return response;
        }
    }
}
