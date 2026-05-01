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

    /// <summary>
    /// Bug fix 2026-04-30: handler must read projektId z query stringu (např. NavrhyController
    /// /Navrhy/CreateScheduleProposal?projektId=...). Před fixem dostal null → global check
    /// → 403 i pro user s VLASTNIK_PROJEKTU/PROJ_MAN rolí.
    /// </summary>
    [Fact]
    public async Task Handler_ShouldPassProjektIdFromQuery_WhenPresent()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "proposals.schedule.create", 555, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?projektId=555&zaznamId=10");
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns(httpContext);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("proposals.schedule.create");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        authzService.Verify(s => s.HasPermissionAsync(42, "proposals.schedule.create", 555, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// POST endpointy s [FromForm] commandy (např. SubmitScheduleProposal) — projektId
    /// pochází z form body, ne z route ani query.
    /// </summary>
    [Fact]
    public async Task Handler_ShouldPassProjektIdFromForm_WhenPresent()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "proposals.record.create", 999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Method = "POST";
        var formData = new System.Collections.Generic.Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "projektId", "999" }
        };
        httpContext.Request.Form = new FormCollection(formData);
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns(httpContext);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("proposals.record.create");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        authzService.Verify(s => s.HasPermissionAsync(42, "proposals.record.create", 999, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Route má precedenci nad query — pokud route value existuje, query se ignoruje.
    /// Zabrání cross-source confusion attack (user injectuje projektId do query, ale
    /// route má jiný projektId z URL).
    /// </summary>
    [Fact]
    public async Task Handler_RouteShouldTakePrecedenceOverQuery()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        authzService
            .Setup(s => s.HasPermissionAsync(42, "records.edit", 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["projektId"] = "100";
        httpContext.Request.QueryString = new QueryString("?projektId=200");
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns(httpContext);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
        authzService.Verify(s => s.HasPermissionAsync(42, "records.edit", 100, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// M2 invariant rozšířený na query: malformed projektId v query → fail closed.
    /// </summary>
    [Fact]
    public async Task Handler_ShouldFail_WhenQueryProjektIdIsMalformed()
    {
        var authzService = new Mock<IPmTrackerAuthzService>();
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(42);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString("?projektId=abc");
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(h => h.HttpContext).Returns(httpContext);

        var handler = new PermissionAuthorizationHandler(authzService.Object, currentUser.Object, httpContextAccessor.Object);
        var requirement = new PermissionRequirement("records.edit");
        var context = new AuthorizationHandlerContext(new[] { requirement }, new ClaimsPrincipal(), null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        authzService.VerifyNoOtherCalls(); // ne-zavolal HasPermissionAsync
    }
}
