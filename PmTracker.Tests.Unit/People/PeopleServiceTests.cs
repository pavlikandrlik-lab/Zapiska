using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.People;

namespace PmTracker.Tests.Unit.People;

public sealed class PeopleServiceTests
{
    [Fact]
    public void BuildOsoby_ShouldDelegateToDataStore()
    {
        var expected = new OsobyIndexViewModel
        {
            Osoby = [],
            Organizace = [],
            OrganizacniCelky = []
        };
        var dataStore = new FakePeopleDataStore(expected);
        var sut = new PeopleService(dataStore);

        var result = sut.BuildOsoby();

        result.Should().BeSameAs(expected);
    }

    private sealed class FakePeopleDataStore(OsobyIndexViewModel people) : IPeopleDataStore
    {
        public OsobyIndexViewModel BuildOsoby() => people;

        public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser) => 1;

        public int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser) => 1;

        public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser)
        {
        }
    }
}
