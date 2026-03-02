namespace PmTracker.Web.Services.Common;

public interface IPersonIdentityMatcher
{
    bool NameEquals(string? titul, string jmeno, string prijmeni, string normalizedSearch);
    bool NameContains(string? titul, string jmeno, string prijmeni, string normalizedSearch);
    bool EmailEquals(string? email, string normalizedSearch);
    bool EmailContains(string? email, string normalizedSearch);
    string BuildPersonName(string? titul, string jmeno, string prijmeni);
}
