using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class DisabledTicketingQueryServiceTests
{
    private static DisabledTicketingQueryService CreateService()
        => new(NullLogger<DisabledTicketingQueryService>.Instance);

    [Fact]
    public async Task VsechnyMetody_VraciPrazdny()
    {
        var svc = CreateService();

        (await svc.GetZaznamAsync("A", default)).Should().BeNull();
        (await svc.GetZaznamyAsync(new[] { "A", "B" }, default)).Should().BeEmpty();
        (await svc.GetAkceptovanouKalkulaciAsync("A", default)).Should().BeNull();
        (await svc.GetAkceptovaneKalkulaceAsync(new[] { "A", "B" }, default)).Should().BeEmpty();
    }
}
