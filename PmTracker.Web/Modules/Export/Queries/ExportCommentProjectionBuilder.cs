using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Export.Queries;

public sealed class ExportCommentProjectionBuilder(IRichTextContentService richTextContentService) : IExportCommentProjectionBuilder
{
    public List<VyjadreniEntity> OrderForExport(IReadOnlyList<VyjadreniEntity> comments, IReadOnlyDictionary<int, JednaniEntity> meetingById)
    {
        return comments
            .OrderBy(comment => meetingById.GetValueOrDefault(comment.JednaniId)?.CisloJednani ?? 0)
            .ThenBy(comment => comment.Id)
            .ToList();
    }

    public List<VyjadreniEntity> ApplyLimit(IReadOnlyList<VyjadreniEntity> comments)
    {
        const int lineBudgetPerTask = 20;
        const int estimatedCharsPerLine = 95;

        var selected = new List<VyjadreniEntity>();
        var usedLines = 0;

        for (var index = comments.Count - 1; index >= 0; index--)
        {
            var comment = comments[index];
            var commentPlainText = richTextContentService.ToPlainText(comment.TextVyjadreni);
            var estimatedTextLines = EstimateCommentTextLines(commentPlainText, estimatedCharsPerLine);
            var estimatedLines = 1 + estimatedTextLines;

            if (selected.Count > 0 && usedLines + estimatedLines > lineBudgetPerTask)
            {
                break;
            }

            selected.Add(comment);
            usedLines += estimatedLines;
        }

        selected.Reverse();
        return selected;
    }

    private static int EstimateCommentTextLines(string plainText, int estimatedCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return 1;
        }

        var normalized = plainText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var estimatedLines = 0;
        var rows = normalized.Split('\n');
        foreach (var row in rows)
        {
            estimatedLines += Math.Max(1, (int)Math.Ceiling(row.Length / (double)estimatedCharsPerLine));
        }

        return Math.Max(1, estimatedLines);
    }
}
