using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Server autoritativně dohledá Typ externí vazby ze ServiceDesku podle čísla tiketu (návrh
/// založení). Ticket nenalezen / SD vypnuté → validační chyba (žádný neschvalitelný návrh).
/// </summary>
public sealed class ExternalLinkProposalTypeResolverTests
{
    private sealed class FakeTicketing : ITicketingQueryService
    {
        private readonly IReadOnlyDictionary<string, string?> _typeByCislo;
        public FakeTicketing(IReadOnlyDictionary<string, string?> typeByCislo) => _typeByCislo = typeByCislo;

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult(_typeByCislo.TryGetValue(cislo, out var typ)
                ? new HotZaznamDto(cislo, typ, null, null)
                : null);

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(IReadOnlyCollection<string> cisla, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(IReadOnlyCollection<string> cisla, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private static ExternalLinkProposalTypeResolver Resolver(params (string cislo, string? typ)[] tickets)
        => new(new FakeTicketing(tickets.ToDictionary(t => t.cislo, t => t.typ)));

    private static SaveRecordExterniVazbaCommand Link(string? cislo, string? typ = null)
        => new() { Cislo = cislo, Typ = typ };

    [Fact]
    public async Task Resolves_Type_FromTicket_AndOverwritesClientValue()
    {
        var link = Link("334565", typ: "wrong-from-client");
        var resolver = Resolver(("334565", "PMP  ")); // CHAR(5) padding jako v HOT_ZAZNAMY

        await resolver.ResolveAndValidateAsync(new[] { link });

        link.Typ.Should().Be("PMP", "server normalizuje (Trim+Upper) a je autoritativní zdroj Typu");
    }

    [Fact]
    public async Task Throws_When_TicketNotFound()
    {
        var resolver = Resolver(); // SD nezná žádný ticket

        var act = () => resolver.ResolveAndValidateAsync(new[] { Link("999999") });

        await act.Should().ThrowAsync<RecordValidationException>()
            .Where(ex => ex.Issues.Any(i => i.Rule == "external_sd_ticket_not_found"));
    }

    [Fact]
    public async Task Throws_When_CisloNotSixDigits()
    {
        var resolver = Resolver(("123456", "PMP"));

        var act = () => resolver.ResolveAndValidateAsync(new[] { Link("abc") });

        await act.Should().ThrowAsync<RecordValidationException>()
            .Where(ex => ex.Issues.Any(i => i.Rule == "external_number_required"));
    }

    [Fact]
    public async Task EmptyRow_IsSkipped()
    {
        var resolver = Resolver();
        var act = () => resolver.ResolveAndValidateAsync(new[] { Link(cislo: "", typ: "") });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NoLinks_IsNoOp()
    {
        var resolver = Resolver();
        var act = () => resolver.ResolveAndValidateAsync(Array.Empty<SaveRecordExterniVazbaCommand>());
        await act.Should().NotThrowAsync();
    }
}
