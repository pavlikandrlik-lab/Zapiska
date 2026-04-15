using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

internal static class RecordCategoryClassifier
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public static bool IsTaskCategory(string? categoryCode, string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode)
            && (Ci.Equals(categoryCode.Trim(), RecordCategoryCodes.TaskShort)
                || Ci.Equals(categoryCode.Trim(), RecordCategoryCodes.Task)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        return categoryName.Contains("úkol", StringComparison.OrdinalIgnoreCase)
            || categoryName.Contains("ukol", StringComparison.OrdinalIgnoreCase);
    }
}

