using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Services.ActiveDirectory;

namespace PmTracker.Tests.Unit.Common;

public sealed class ActiveDirectoryServiceTests
{
    [Fact]
    public async Task SearchUsersAsync_ShouldReturnEmptyResults_WhenQueryIsBlank()
    {
        var sut = CreateSut(new ActiveDirectoryOptions
        {
            Domain = "acr",
            MaxResults = 15,
            QueryTimeoutSeconds = 8
        });

        var response = await sut.SearchUsersAsync("   ");

        response.Available.Should().BeTrue();
        response.Message.Should().BeNull();
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchUsersAsync_ShouldReturnUnavailableMessage_WhenAdBackendIsUnavailable()
    {
        var sut = CreateSut(new ActiveDirectoryOptions
        {
            Domain = "unit-test-ad-unavailable",
            MaxResults = 10,
            QueryTimeoutSeconds = 2
        });

        var response = await sut.SearchUsersAsync("john.smith");

        response.Available.Should().BeFalse();
        response.Results.Should().BeEmpty();
        response.Message.Should().Be("Active Directory UNIT-TEST-AD-UNAVAILABLE není dostupné");
    }

    [Fact]
    public void RankResults_ShouldPrioritizeExactEmailMatch()
    {
        var expectedTop = Guid.NewGuid();
        var other = Guid.NewGuid();
        var extra = Guid.NewGuid();

        var users = new[]
        {
            CreateAdInfo(
                expectedTop,
                displayName: "Jan Novak",
                firstName: "Jan",
                surname: "Novak",
                mail: "jan.novak@pmtracker.test",
                login: "acr\\jan.novak",
                company: "MO",
                department: "OI"),
            CreateAdInfo(
                other,
                displayName: "Janina Nova",
                firstName: "Janina",
                surname: "Nova",
                mail: "janina.nova@pmtracker.test",
                login: "acr\\janina.nova",
                company: "MO",
                department: "OI"),
            CreateAdInfo(
                extra,
                displayName: "Petr Novak",
                firstName: "Petr",
                surname: "Novak",
                mail: "petr.novak@pmtracker.test",
                login: "acr\\petr.novak",
                company: "MO",
                department: "OI")
        };

        var ranked = InvokeRankResults(users, "jan.novak@pmtracker.test", maxResults: 10);

        ranked.Should().NotBeEmpty();
        ranked[0].GuidAd.Should().Be(expectedTop);
    }

    private static ActiveDirectoryService CreateSut(ActiveDirectoryOptions options)
    {
        return new ActiveDirectoryService(
            Options.Create(options),
            NullLogger<ActiveDirectoryService>.Instance);
    }

    private static object CreateAdInfo(
        Guid guid,
        string displayName,
        string firstName,
        string surname,
        string mail,
        string login,
        string? company,
        string? department)
    {
        var adInfoType = ResolveAdInfoType();
        var instance = Activator.CreateInstance(adInfoType);
        instance.Should().NotBeNull();

        SetProperty(adInfoType, instance!, "GuidAd", guid);
        SetProperty(adInfoType, instance!, "DisplayName", displayName);
        SetProperty(adInfoType, instance!, "FirstName", firstName);
        SetProperty(adInfoType, instance!, "Surname", surname);
        SetProperty(adInfoType, instance!, "Mail", mail);
        SetProperty(adInfoType, instance!, "Login", login);
        SetProperty(adInfoType, instance!, "Company", company);
        SetProperty(adInfoType, instance!, "Department", department);

        return instance!;
    }

    private static IReadOnlyList<ActiveDirectoryPersonResult> InvokeRankResults(
        IReadOnlyList<object> users,
        string query,
        int maxResults)
    {
        var adInfoType = ResolveAdInfoType();
        var typedListType = typeof(List<>).MakeGenericType(adInfoType);
        var typedList = Activator.CreateInstance(typedListType);
        typedList.Should().NotBeNull();

        var addMethod = typedListType.GetMethod("Add");
        addMethod.Should().NotBeNull();
        foreach (var user in users)
        {
            addMethod!.Invoke(typedList, new[] { user });
        }

        var rankResultsMethod = typeof(ActiveDirectoryService).GetMethod(
            "RankResults",
            BindingFlags.NonPublic | BindingFlags.Static);
        rankResultsMethod.Should().NotBeNull();

        var result = rankResultsMethod!.Invoke(null, new object?[] { typedList, query, maxResults });
        result.Should().NotBeNull();

        return ((IEnumerable<ActiveDirectoryPersonResult>)result!).ToList();
    }

    private static Type ResolveAdInfoType()
    {
        var adInfoType = typeof(ActiveDirectoryService).Assembly.GetType("PmTracker.Web.Services.ActiveDirectory.ADInfo");
        adInfoType.Should().NotBeNull();
        return adInfoType!;
    }

    private static void SetProperty(Type targetType, object instance, string propertyName, object? value)
    {
        var property = targetType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property.Should().NotBeNull();
        property!.SetValue(instance, value);
    }
}
