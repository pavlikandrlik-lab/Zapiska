namespace PmTracker.Web.Services.Common;

public interface IRichTextContentService
{
    string NormalizeForStorage(string? value);
    string ToSafeHtml(string? value);
    string ToPlainText(string? value);
    bool HasVisibleText(string? value);
}
