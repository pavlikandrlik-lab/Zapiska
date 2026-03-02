using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;

namespace PmTracker.Web.Services.ActiveDirectory;

internal static class ADConnector
{
    public static IReadOnlyList<ADInfo> GetADUsers(string query, string domain, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ADInfo>();
        }

        var domainName = string.IsNullOrWhiteSpace(domain) ? "acr" : domain.Trim();
        var limit = Math.Max(5, maxResults);
        var result = new Dictionary<Guid, ADInfo>();

        using var context = new PrincipalContext(ContextType.Domain, domainName);
        SearchAndCollect(context, p => p.DisplayName = query + "*", result, limit);
        SearchAndCollect(context, p => p.DisplayName = "*" + query + "*", result, limit);
        SearchAndCollect(context, p => p.GivenName = query + "*", result, limit);
        SearchAndCollect(context, p => p.Surname = query + "*", result, limit);
        SearchAndCollect(context, p => p.EmailAddress = query + "*", result, limit);
        SearchAndCollect(context, p => p.SamAccountName = query + "*", result, limit);

        return result.Values.ToList();
    }

    private static void SearchAndCollect(
        PrincipalContext context,
        Action<UserPrincipal> configureProbe,
        IDictionary<Guid, ADInfo> collector,
        int maxResults)
    {
        if (collector.Count >= maxResults)
        {
            return;
        }

        using var probe = new UserPrincipal(context) { Enabled = true };
        configureProbe(probe);
        using var searcher = new PrincipalSearcher(probe);

        foreach (var principal in searcher.FindAll())
        {
            if (collector.Count >= maxResults)
            {
                break;
            }

            if (principal is not UserPrincipal userPrincipal)
            {
                continue;
            }

            using (userPrincipal)
            {
                var info = ToAdInfo(userPrincipal);
                if (info is null)
                {
                    continue;
                }

                collector[info.GuidAd] = info;
            }
        }
    }

    private static ADInfo? ToAdInfo(UserPrincipal user)
    {
        if (!user.Guid.HasValue)
        {
            return null;
        }

        var directoryEntry = user.GetUnderlyingObject() as DirectoryEntry;
        var login = user.SamAccountName;
        if (user.Sid is not null)
        {
            try
            {
                login = user.Sid.Translate(typeof(NTAccount)).ToString();
            }
            catch
            {
                // Keep samAccountName fallback.
            }
        }

        var displayName = string.IsNullOrWhiteSpace(user.DisplayName)
            ? string.Join(" ", new[] { user.GivenName, user.Surname }.Where(x => !string.IsNullOrWhiteSpace(x)))
            : user.DisplayName;

        return new ADInfo
        {
            GuidAd = user.Guid.Value,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? login ?? user.Name ?? string.Empty : displayName,
            FirstName = user.GivenName ?? string.Empty,
            Surname = user.Surname ?? string.Empty,
            Titul = ReadDirectoryValue(directoryEntry, "title"),
            Company = ReadDirectoryValue(directoryEntry, "company"),
            Department = ReadDirectoryValue(directoryEntry, "department"),
            Office = ReadDirectoryValue(directoryEntry, "physicalDeliveryOfficeName"),
            Mail = user.EmailAddress ?? string.Empty,
            EmployeeId = user.EmployeeId ?? string.Empty,
            Telephone = user.VoiceTelephoneNumber ?? string.Empty,
            Description = user.Description ?? string.Empty,
            Login = login ?? string.Empty
        };
    }

    private static string? ReadDirectoryValue(DirectoryEntry? entry, string propertyName)
    {
        if (entry is null)
        {
            return null;
        }

        if (!entry.Properties.Contains(propertyName))
        {
            return null;
        }

        var value = entry.Properties[propertyName].Value;
        return value?.ToString();
    }
}

internal sealed class ADInfo
{
    public Guid GuidAd { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string? Titul { get; init; }
    public string? Company { get; init; }
    public string? Department { get; init; }
    public string? Office { get; init; }
    public string Mail { get; init; } = string.Empty;
    public string EmployeeId { get; init; } = string.Empty;
    public string Telephone { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
}
