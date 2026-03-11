using FluentAssertions;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Unit.Data;

public sealed class RecordValidationExceptionTests
{
    [Fact]
    public void RecordValidationException_ShouldAggregateDistinctFieldErrors_ByFieldKey()
    {
        var issues = new List<RecordValidationIssue>
        {
            new("Popis", "Prvni chyba", "basic", "rule_1", "value-1"),
            new("popis", "Prvni chyba", "basic", "rule_1", "value-1"),
            new("Popis", "Druha chyba", "basic", "rule_2", "value-2"),
            new("ExterniVazby[0].Cislo", "Cislo je povinne", "external", "rule_3", string.Empty)
        };

        var exception = new RecordValidationException("Validation failed.", issues, "diagnostic log");

        exception.ErrorCode.Should().Be(AjaxErrorCodes.RecordValidationFailed);
        exception.FieldErrors.Keys.Should().Contain("Popis");
        exception.FieldErrors.Keys.Should().Contain("ExterniVazby[0].Cislo");
        exception.FieldErrors["Popis"].Should().BeEquivalentTo(new[] { "Prvni chyba", "Druha chyba" });
        exception.DiagnosticLog.Should().Be("diagnostic log");
        exception.Issues.Should().HaveCount(4);
    }
}
