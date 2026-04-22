using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.ExterniOdkaz;
using PmTracker.Web.Services.Security;
using Xunit;
using IPmAuthorizationService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniOdkazSyncControllerTests
{
    private static readonly int OsobaId = 42;
    private static readonly int ProjektId = 7;

    private static (ExterniOdkazController sut, Mock<ITicketingQueryService> ticketing, Mock<IPmAuthorizationService> authz)
        CreateSut(bool hasPermission = true)
    {
        var ticketing = new Mock<ITicketingQueryService>();
        var authz = new Mock<IPmAuthorizationService>();
        authz.Setup(x => x.HasPermissionAsync(OsobaId, PermissionKeys.RecordsEdit, ProjektId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPermission);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(x => x.OsobaId).Returns(OsobaId);

        var sut = new ExterniOdkazController(ticketing.Object, authz.Object, currentUser.Object);
        return (sut, ticketing, authz);
    }

    [Fact]
    public async Task Sync_WithInvalidCislo_ReturnsBadRequest()
    {
        var (sut, _, _) = CreateSut();

        var result = await sut.Sync("abc", ProjektId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Sync_WithoutPermission_ReturnsForbid()
    {
        var (sut, _, _) = CreateSut(hasPermission: false);

        var result = await sut.Sync("123456", ProjektId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Sync_WithSixDigitCisloNotFound_ReturnsNotFoundResponse()
    {
        var (sut, ticketing, _) = CreateSut();
        ticketing.Setup(x => x.GetZaznamAsync("999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((HotZaznamDto?)null);

        var result = await sut.Sync("999999", ProjektId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ExterniOdkazSyncResponse>().Subject;
        payload.Nalezeno.Should().BeFalse();
        payload.Typ.Should().BeNull();
        payload.Cislo.Should().Be("999999");
    }

    [Fact]
    public async Task Sync_WithSixDigitCisloFound_ReturnsTypAndStrucne()
    {
        var (sut, ticketing, _) = CreateSut();
        ticketing.Setup(x => x.GetZaznamAsync("336865", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HotZaznamDto("336865", "PNF", "Oprava přihlášení", null));

        var result = await sut.Sync("336865", ProjektId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ExterniOdkazSyncResponse>().Subject;
        payload.Nalezeno.Should().BeTrue();
        payload.Typ.Should().Be("PNF");
        payload.Strucne.Should().Be("Oprava přihlášení");
    }
}
