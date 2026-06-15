using FluentAssertions;
using PmTracker.Web.Services.Data;
using Xunit;

namespace PmTracker.Tests.Unit.Data;

/// <summary>
/// Datum-model migrace: startup schema guard musí vyžadovat novou tabulku
/// <c>zaznam_harmonogram_krok</c> (jinak deploy bez db_upgrade_1_4_0 nastartuje a spadne
/// až za běhu). Stará offset tabulka <c>zaznam_harmonogram_hodnoty</c> byla migrací DROPnuta,
/// takže ji guard už nesmí vyžadovat ani validovat.
/// </summary>
public sealed class SqlStartupValidatorRequiredTablesTests
{
    [Fact]
    public void RequiredTables_ShouldRequireDatumModelKrokTable()
    {
        SqlStartupValidatorHostedService.RequiredTables
            .Should().Contain("dbo.zaznam_harmonogram_krok");
    }

    [Fact]
    public void RequiredTables_ShouldNotReferenceDroppedOffsetTable()
    {
        SqlStartupValidatorHostedService.RequiredTables
            .Should().NotContain("dbo.zaznam_harmonogram_hodnoty");
    }
}
