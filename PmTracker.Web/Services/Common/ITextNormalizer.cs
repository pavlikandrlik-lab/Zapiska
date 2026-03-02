namespace PmTracker.Web.Services.Common;

public interface ITextNormalizer
{
    string Normalize(string value);
    string? NormalizeEmail(string? value);
}
