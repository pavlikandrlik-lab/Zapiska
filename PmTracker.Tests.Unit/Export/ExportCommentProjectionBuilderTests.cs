using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Modules.Export.Queries;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Export;

public sealed class ExportCommentProjectionBuilderTests
{
    private readonly ExportCommentProjectionBuilder _sut = new(new PassthroughRichTextContentService());

    [Fact]
    public void OrderForExport_ShouldOrderByMeetingNumber_ThenCommentId()
    {
        var comments = new List<VyjadreniEntity>
        {
            new() { Id = 3, JednaniId = 10, TextVyjadreni = "c3" },
            new() { Id = 1, JednaniId = 11, TextVyjadreni = "c1" },
            new() { Id = 2, JednaniId = 10, TextVyjadreni = "c2" }
        };
        var meetings = new Dictionary<int, JednaniEntity>
        {
            [10] = new JednaniEntity { Id = 10, CisloJednani = 2 },
            [11] = new JednaniEntity { Id = 11, CisloJednani = 1 }
        };

        var result = _sut.OrderForExport(comments, meetings);

        result.Select(x => x.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ApplyLimit_ShouldKeepNewestCommentsWithinLineBudget()
    {
        var comments = Enumerable.Range(1, 12)
            .Select(index => new VyjadreniEntity
            {
                Id = index,
                TextVyjadreni = $"comment {index}"
            })
            .ToList();

        var result = _sut.ApplyLimit(comments);

        result.Select(x => x.Id).Should().Equal(3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
    }

    private sealed class PassthroughRichTextContentService : IRichTextContentService
    {
        public string NormalizeForStorage(string? value) => value ?? string.Empty;

        public string ToSafeHtml(string? value) => value ?? string.Empty;

        public string ToPlainText(string? value) => value ?? string.Empty;

        public bool HasVisibleText(string? value) => !string.IsNullOrWhiteSpace(value);
    }
}
