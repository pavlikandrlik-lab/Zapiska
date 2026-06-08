using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Controllers;

/// <summary>
/// QW-7 (revised 2026-05-04): The AJAX failure payload now exposes a sanitized
/// <c>DiagnosticLog</c> on <see cref="ModalSubmitResultViewModel"/> so that users
/// can save full SQL/EFCore error details from the UI for debugging. The sanitization
/// invariant is preserved at the source (BuildDiagnosticLog): the log must contain
/// only TimestampUtc, ErrorCode, TraceId, Request method+path+query, Message,
/// FieldErrors (localized labels — no values), Details, and Exception/SQL details.
/// It must NOT serialize <c>HttpContext.Request.Form</c> body values.
/// </summary>
public sealed class BaseControllerAjaxPrivacyTests
{
    /// <summary>
    /// When the HTTP request carries form data with PII, the diagnostic log must
    /// not echo any submitted form values back to the client.
    /// </summary>
    [Fact]
    public void BuildAjaxFailurePayload_DoesNotIncludeFormValues_WhenRequestHasFormContent()
    {
        // Arrange — controller with a form-backed request containing a PII field
        const string piiEmail = "test@example.com";

        var formCollection = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["email"] = piiEmail,
                ["jmeno"] = "Jan Novak"
            });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = formCollection;

        var controller = CreateController(httpContext);

        // Act
        var result = controller.InvokeAjaxErrorResult("Operace selhala.", details: "test-details");

        // Assert
        var vm = result.Value.Should().BeOfType<ModalSubmitResultViewModel>().Subject;

        // DiagnosticLog is now exposed (QW-7 revised) but MUST NOT contain form values.
        typeof(ModalSubmitResultViewModel)
            .GetProperty("DiagnosticLog")
            .Should().NotBeNull(
                because: "DiagnosticLog je vystaven klientovi pro UI ukládání chyb (QW-7 revised 2026-05-04)");

        vm.Message.Should().NotContain(piiEmail);
        vm.DiagnosticLog.Should().NotBeNullOrWhiteSpace();
        vm.DiagnosticLog.Should().NotContain(piiEmail,
            because: "BuildDiagnosticLog nesmí obsahovat hodnoty form polí (QW-7 invariant zachován u zdroje)");
        vm.DiagnosticLog.Should().NotContain("Jan Novak",
            because: "BuildDiagnosticLog nesmí obsahovat hodnoty form polí (QW-7 invariant zachován u zdroje)");
        vm.Ok.Should().BeFalse();
    }

    /// <summary>
    /// Regression guard: TraceId must still be present in the failure payload
    /// so that users can quote it back to support.
    /// </summary>
    [Fact]
    public void BuildAjaxFailurePayload_StillIncludesTraceId()
    {
        // Arrange
        const string expectedTraceId = "test-trace-id-12345";
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = expectedTraceId;

        var controller = CreateController(httpContext);

        // Act
        var result = controller.InvokeAjaxErrorResult("Operace selhala.");

        // Assert
        var vm = result.Value.Should().BeOfType<ModalSubmitResultViewModel>().Subject;
        vm.TraceId.Should().Be(expectedTraceId);
        vm.Ok.Should().BeFalse();
    }

    private static TestBaseController CreateController(HttpContext httpContext)
    {
        var controller = new TestBaseController(
            new StubUserContextResolver(
                UserContextResolutionResult.Unauthorized("test")))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
        return controller;
    }

    /// <summary>
    /// Minimal concrete subclass that exposes the protected AJAX helpers for testing.
    /// </summary>
    private sealed class TestBaseController(IUserContextResolver userContextResolver)
        : BaseController(userContextResolver, TimeProvider.System, NullLoggerFactory.Instance)
    {
        public BadRequestObjectResult InvokeAjaxErrorResult(string message, string? details = null)
            => AjaxErrorResult(message, details: details);
    }

    private sealed class StubUserContextResolver(UserContextResolutionResult resolution) : IUserContextResolver
    {
        public Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
            => Task.FromResult(resolution);
    }
}
