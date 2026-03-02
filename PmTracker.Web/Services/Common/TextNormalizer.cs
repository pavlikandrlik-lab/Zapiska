using System.Globalization;
using System.Text;

namespace PmTracker.Web.Services.Common;

public sealed class TextNormalizer : ITextNormalizer
{
    public string Normalize(string value)
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

    public string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
