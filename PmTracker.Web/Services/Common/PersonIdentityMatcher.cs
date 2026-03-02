namespace PmTracker.Web.Services.Common;

public sealed class PersonIdentityMatcher : IPersonIdentityMatcher
{
    private readonly ITextNormalizer _textNormalizer;

    public PersonIdentityMatcher(ITextNormalizer textNormalizer)
    {
        _textNormalizer = textNormalizer;
    }

    public bool NameEquals(string? titul, string jmeno, string prijmeni, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        var withTitle = _textNormalizer.Normalize(BuildPersonName(titul, jmeno, prijmeni));
        if (string.Equals(withTitle, normalizedSearch, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var withoutTitle = _textNormalizer.Normalize(BuildPersonName(null, jmeno, prijmeni));
        return string.Equals(withoutTitle, normalizedSearch, StringComparison.OrdinalIgnoreCase);
    }

    public bool NameContains(string? titul, string jmeno, string prijmeni, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        var withTitle = _textNormalizer.Normalize(BuildPersonName(titul, jmeno, prijmeni));
        if (ContainsEitherDirection(withTitle, normalizedSearch))
        {
            return true;
        }

        var withoutTitle = _textNormalizer.Normalize(BuildPersonName(null, jmeno, prijmeni));
        return ContainsEitherDirection(withoutTitle, normalizedSearch);
    }

    public bool EmailEquals(string? email, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        var normalizedEmail = _textNormalizer.Normalize(email ?? string.Empty);
        return string.Equals(normalizedEmail, normalizedSearch, StringComparison.OrdinalIgnoreCase);
    }

    public bool EmailContains(string? email, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
        {
            return false;
        }

        var normalizedEmail = _textNormalizer.Normalize(email ?? string.Empty);
        return ContainsEitherDirection(normalizedEmail, normalizedSearch);
    }

    public string BuildPersonName(string? titul, string jmeno, string prijmeni)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(titul))
        {
            parts.Add(titul.Trim());
        }

        if (!string.IsNullOrWhiteSpace(jmeno))
        {
            parts.Add(jmeno.Trim());
        }

        if (!string.IsNullOrWhiteSpace(prijmeni))
        {
            parts.Add(prijmeni.Trim());
        }

        return string.Join(" ", parts);
    }

    private static bool ContainsEitherDirection(string value, string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
            || normalizedSearch.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
