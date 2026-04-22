using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using PmTracker.Web.Services.Security;
using System.Security.Claims;

// Alias to resolve naming collision with Microsoft.AspNetCore.Authorization.IAuthorizationService
using IPmTrackerAuthzService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Tests.Unit.Services.Security;

/// <summary>
/// Edge-case tests for PermissionAuthorizationHandler route-value parsing (M2/M4).
/// - Malformed (present but unparseable) projektId/projektSubsystemId must fail closed.
/// - Missing keys must preserve existing global-scope behaviour.
/// </summary>
public sealed class PermissionAuthorizationHandlerEdgeCasesTests
{
    private static PermissionAuthorizationHandler BuildHandler(
        Mock<IPmTrackerAuthzService> authzService,
        int osobaId,
        HttpContext? httpContext = null)
    {
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(osobaId);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns(httpContext);

        return new PermissionAuthorizationHandler(
            authzService.Object,
            currentUser.Object,
            httpContextAccessor.Object);
    }

    private static AuthorizationHandlerContext BuildAuthContext(string permissionKey)
    {
        var requirement = new PermissionRequirement(permissionKey);
        return new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(),
            null);
    }

    // ---------------------------------------------------------------------------
    // M2 — malformed projektId must fail closed
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleRequirementAsync_WhenProjektIdIsMalformed_DoesNotSucceed()
    {
        // Arrange
        const string permKey = "records.edit";
        var authzService = new Mock<IPmTrackerAuthzService>();
        // Even if authzService would grant permission globally, handler must not call it
        authzService
            .Setup(s => s.HasPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["projektId"] = "abc"; // present but unparseable

        var handler = BuildHandler(authzService, osobaId: 42, httpContext: httpContext);
        var ctx = BuildAuthContext(permKey);

        // Act
        await handler.HandleAsync(ctx);

        // Assert — malformed projektId must fail closed, HasPermissionAsync must NOT be called
        ctx.HasSucceeded.Should().BeFalse("malformed projektId must prevent authorization (fail closed)");
        authzService.Verify(
            s => s.HasPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "HasPermissionAsync must not be called when projektId is malformed");
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenProjektIdMissing_DoesGlobalCheck()
    {
        // Arrange — regression guard: missing key must still do a global-scope check
        const string permKey = "records.view";
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, permKey, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // httpContext has no "projektId" key
        var httpContext = new DefaultHttpContext();

        var handler = BuildHandler(authzService, osobaId: 42, httpContext: httpContext);
        var ctx = BuildAuthContext(permKey);

        // Act
        await handler.HandleAsync(ctx);

        // Assert — should succeed via global check (projektId=null)
        ctx.HasSucceeded.Should().BeTrue("missing projektId must fall through to global-scope check");
        authzService.Verify(
            s => s.HasPermissionAsync(42, permKey, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------------------
    // M4 — route key renamed: projektSubsystemId (replaces subsystemId)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task HandleRequirementAsync_WhenProjektSubsystemIdIsMalformed_DoesNotSucceed()
    {
        // Arrange
        const string permKey = "subsystem.edit";
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["projektSubsystemId"] = "not-a-number"; // present but unparseable

        var handler = BuildHandler(authzService, osobaId: 42, httpContext: httpContext);
        var ctx = BuildAuthContext(permKey);

        // Act
        await handler.HandleAsync(ctx);

        // Assert
        ctx.HasSucceeded.Should().BeFalse("malformed projektSubsystemId must prevent authorization (fail closed)");
        authzService.Verify(
            s => s.HasPermissionAsync(
                It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "HasPermissionAsync must not be called when projektSubsystemId is malformed");
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenProjektSubsystemIdValid_PassesToAuthzService()
    {
        // Arrange — new key name "projektSubsystemId" must be read (M4 rename)
        const string permKey = "subsystem.view";
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, permKey, null, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["projektSubsystemId"] = "99";

        var handler = BuildHandler(authzService, osobaId: 42, httpContext: httpContext);
        var ctx = BuildAuthContext(permKey);

        // Act
        await handler.HandleAsync(ctx);

        // Assert — new key name must be read correctly
        ctx.HasSucceeded.Should().BeTrue();
        authzService.Verify(
            s => s.HasPermissionAsync(42, permKey, null, 99, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
