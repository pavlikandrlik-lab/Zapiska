using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using PmTracker.Web.Services.Security;
using System.Security.Claims;

// Alias to resolve naming collision with Microsoft.AspNetCore.Authorization.IAuthorizationService
using IPmTrackerAuthzService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class PermissionPolicyHandlerTests
{
    [Fact]
    public async Task Handler_ShouldSucceed_WhenPermissionGranted()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "records.edit", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns((HttpContext?)null);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(),
            null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_ShouldFail_WhenPermissionDenied()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "records.edit", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns((HttpContext?)null);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(),
            null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_ShouldFail_WhenNoOsobaId()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns((int?)null);
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns((HttpContext?)null);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        authzService.VerifyNoOtherCalls(); // didn't call HasPermissionAsync
    }

    [Fact]
    public async Task Handler_ShouldPassProjektIdFromRoute_WhenPresent()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "records.edit", 777, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["projektId"] = "777";
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns(httpContext);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        authzService.Verify(s => s.HasPermissionAsync(42, "records.edit", 777, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
