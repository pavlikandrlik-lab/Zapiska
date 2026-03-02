using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class ActiveDirectoryService : IActiveDirectoryService
{
    private readonly ActiveDirectoryOptions _options;
    private readonly ILogger<ActiveDirectoryService> _logger;

    public ActiveDirectoryService(
        IOptions<ActiveDirectoryOptions> options,
        ILogger<ActiveDirectoryService> logger)
    {
        _options = options.Value ?? new ActiveDirectoryOptions();
        _logger = logger;
    }

    public async Task<ActiveDirectorySearchResponse> SearchUsersAsync(string? query, CancellationToken cancellationToken = default)
    {
        var rawQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return new ActiveDirectorySearchResponse
            {
                Available = true,
                Results = Array.Empty<ActiveDirectoryPersonResult>()
            };
        }

        var maxResults = Math.Clamp(_options.MaxResults, 5, 50);
        var timeoutSeconds = Math.Clamp(_options.QueryTimeoutSeconds, 2, 30);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var users = await Task.Run(
                () => ADConnector.GetADUsers(rawQuery, _options.Domain, maxResults * 2),
                timeoutCts.Token);

            var ranked = RankResults(users, rawQuery, maxResults);
            return new ActiveDirectorySearchResponse
            {
                Available = true,
                Results = ranked
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AD search timeout for query '{Query}'.", rawQuery);
            return NotAvailable(UnavailableMessage(_options.Domain));
        }
        catch (PrincipalServerDownException ex)
        {
            _logger.LogWarning(ex, "AD server is down.");
            return NotAvailable(UnavailableMessage(_options.Domain));
        }
        catch (PlatformNotSupportedException ex)
        {
            _logger.LogWarning(ex, "AD search is not supported on this platform.");
            return NotAvailable(UnavailableMessage(_options.Domain));
        }
        catch (DirectoryServicesCOMException ex)
        {
            _logger.LogWarning(ex, "AD search COM failure.");
            return NotAvailable(UnavailableMessage(_options.Domain));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AD search failed.");
            return NotAvailable(UnavailableMessage(_options.Domain));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected AD search error.");
            return NotAvailable(UnavailableMessage(_options.Domain));
        }
    }

    private static ActiveDirectorySearchResponse NotAvailable(string message)
    {
        return new ActiveDirectorySearchResponse
        {
            Available = false,
            Message = message,
            Results = Array.Empty<ActiveDirectoryPersonResult>()
        };
    }

    private static string UnavailableMessage(string? domain)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "ACR" : domain.Trim().ToUpperInvariant();
        return $"Active Directory {normalizedDomain} není dostupné";
    }

    private static IReadOnlyList<ActiveDirectoryPersonResult> RankResults(
        IReadOnlyList<ADInfo> users,
        string query,
        int maxResults)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<ActiveDirectoryPersonResult>();
        }

        var tokens = normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scored = users
            .Select(user =>
            {
                var displayName = NormalizeDisplayName(user);
                var jmeno = user.FirstName?.Trim() ?? string.Empty;
                var prijmeni = user.Surname?.Trim() ?? string.Empty;
                var email = user.Mail?.Trim() ?? string.Empty;
                var titul = string.IsNullOrWhiteSpace(user.Titul) ? null : user.Titul.Trim();
                var company = string.IsNullOrWhiteSpace(user.Company) ? null : user.Company.Trim();
                var department = string.IsNullOrWhiteSpace(user.Department) ? null : user.Department.Trim();

                var score = ComputeScore(
                    normalizedQuery,
                    tokens,
                    displayName,
                    jmeno,
                    prijmeni,
                    email,
                    company,
                    department);

                return new
                {
                    Score = score,
                    Result = new ActiveDirectoryPersonResult
                    {
                        GuidAd = user.GuidAd,
                        AdLogin = string.IsNullOrWhiteSpace(user.Login) ? null : user.Login.Trim(),
                        DisplayName = displayName,
                        Jmeno = jmeno,
                        Prijmeni = prijmeni,
                        Titul = titul,
                        Email = email,
                        Company = company,
                        Department = department,
                        CanSelect = !string.IsNullOrWhiteSpace(email),
                        DisabledReason = string.IsNullOrWhiteSpace(email) ? "Osoba v AD nemá vyplněný email." : null
                    }
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Result.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxResults)
            .Select(x => x.Result)
            .ToList();

        return scored;
    }

    private static string NormalizeDisplayName(ADInfo user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        var fullName = BuildFullName(user.FirstName, user.Surname);
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user.Login?.Trim() ?? string.Empty;
    }

    private static int ComputeScore(
        string query,
        IReadOnlyList<string> tokens,
        string displayName,
        string jmeno,
        string prijmeni,
        string email,
        string? company,
        string? department)
    {
        var fullName = BuildFullName(jmeno, prijmeni);
        var orgText = string.Join(' ', new[] { company, department }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var score = 0;
        score += ScoreField(email, query, tokens, exactWeight: 1300, prefixWeight: 900, tokenPrefixWeight: 130, containsWeight: 560);
        score += ScoreField(fullName, query, tokens, exactWeight: 1200, prefixWeight: 820, tokenPrefixWeight: 120, containsWeight: 520);
        score += ScoreField(displayName, query, tokens, exactWeight: 1100, prefixWeight: 780, tokenPrefixWeight: 110, containsWeight: 500);
        score += ScoreField(orgText, query, tokens, exactWeight: 260, prefixWeight: 170, tokenPrefixWeight: 35, containsWeight: 110);

        var tokenCoverage = tokens.Count(token =>
            ContainsNormalized(displayName, token)
            || ContainsNormalized(fullName, token)
            || ContainsNormalized(email, token));
        score += tokenCoverage * 45;

        return score;
    }

    private static int ScoreField(
        string? sourceValue,
        string query,
        IReadOnlyList<string> tokens,
        int exactWeight,
        int prefixWeight,
        int tokenPrefixWeight,
        int containsWeight)
    {
        var normalized = Normalize(sourceValue ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        var score = 0;
        if (string.Equals(normalized, query, StringComparison.OrdinalIgnoreCase))
        {
            score = Math.Max(score, exactWeight);
        }

        if (normalized.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score = Math.Max(score, prefixWeight);
        }

        if (ContainsNormalized(normalized, query))
        {
            score = Math.Max(score, containsWeight);
        }

        foreach (var token in tokens)
        {
            if (ContainsWordPrefix(normalized, token))
            {
                score += tokenPrefixWeight;
            }
            else if (ContainsNormalized(normalized, token))
            {
                score += tokenPrefixWeight / 2;
            }
        }

        return score;
    }

    private static string BuildFullName(string? jmeno, string? prijmeni)
    {
        return string.Join(' ', new[] { jmeno, prijmeni }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    }

    private static bool ContainsWordPrefix(string source, string token)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = source
            .Split(new[] { ' ', '.', ',', ';', '-', '_', '/', '\\', '@' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Any(part => part.StartsWith(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsNormalized(string source, string token)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return source.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }
}
