using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Tests.Unit.Services.ServiceDesk;

/// <summary>
/// Plán 3 Feature D — unit testy pro hard constraint validátor nové externí
/// vazby. Pokrývá: invalid format, ticketing disabled, SD unavailable,
/// ticket not found, happy path.
/// </summary>
public sealed class ExterniOdkazValidatorTests
{
    private static ExterniOdkazValidator CreateSubject(
        bool ticketingEnabled = true,
        Dictionary<string, HotZaznamFingerprintDto>? fingerprints = null,
        bool queryThrows = false)
    {
        var options = Options.Create(new TicketingOptions
        {
            Enabled = ticketingEnabled,
            ConnectionStringName = "TicketingReadOnly"
        });

        var mock = new Mock<IVyjadreniQueryService>();
        mock.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<string> cisla, CancellationToken _) =>
            {
                if (queryThrows)
                {
                    throw new InvalidOperationException("SD unavailable");
                }

                return Task.FromResult<IReadOnlyDictionary<string, HotZaznamFingerprintDto>>(
                    fingerprints ?? new Dictionary<string, HotZaznamFingerprintDto>());
            });

        return new ExterniOdkazValidator(
            mock.Object,
            options,
            NullLogger<ExterniOdkazValidator>.Instance);
    }

    [Theory]
    [InlineData("12345")]      // too short
    [InlineData("1234567")]    // too long
    [InlineData("abcdef")]     // non-numeric
    [InlineData("12a456")]     // mixed
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateCreateAsync_InvalidFormat_ReturnsFailWithFormatCode(string? input)
    {
        var validator = CreateSubject();

        var r = await validator.ValidateCreateAsync(input!, CancellationToken.None);

        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeFormat);
        r.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        r.ErrorMessage.Should().Contain("6 cifer");
    }

    [Fact]
    public async Task ValidateCreateAsync_TicketingDisabled_ReturnsFailWithDisabledCode()
    {
        var validator = CreateSubject(ticketingEnabled: false);

        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);

        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeDisabled);
        r.ErrorMessage.Should().Contain("SD integrace");
    }

    [Fact]
    public async Task ValidateCreateAsync_SdUnavailable_ReturnsFailWithUnavailableCode()
    {
        var validator = CreateSubject(queryThrows: true);

        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);

        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeSdUnavailable);
        r.ErrorMessage.Should().Contain("ServiceDesk");
    }

    [Fact]
    public async Task ValidateCreateAsync_TicketNotInHotZaznamy_ReturnsFailWithNotFoundCode()
    {
        var validator = CreateSubject(
            fingerprints: new Dictionary<string, HotZaznamFingerprintDto>());

        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);

        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeNotFound);
        r.ErrorMessage.Should().Contain("123456");
    }

    [Fact]
    public async Task ValidateCreateAsync_TicketExists_ReturnsOk()
    {
        var fp = new HotZaznamFingerprintDto(
            "123456",
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            "otevřeno",
            "PNF");
        var validator = CreateSubject(
            fingerprints: new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["123456"] = fp
            });

        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);

        r.IsValid.Should().BeTrue();
        r.ErrorCode.Should().BeNull();
        r.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateCreateAsync_CancellationRequested_Propagates()
    {
        var options = Options.Create(new TicketingOptions
        {
            Enabled = true,
            ConnectionStringName = "TicketingReadOnly"
        });

        var mock = new Mock<IVyjadreniQueryService>();
        mock.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var validator = new ExterniOdkazValidator(
            mock.Object,
            options,
            NullLogger<ExterniOdkazValidator>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await validator.ValidateCreateAsync("123456", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
