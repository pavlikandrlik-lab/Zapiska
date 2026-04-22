using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Controllers;

/// <summary>
/// QW-7: Verifies that the AJAX failure payload never leaks PII (form field values)
/// to the client. Form values may appear in the server-side diagnostic log, but
/// the <see cref="ModalSubmitResultViewModel"/> returned to the caller must not
/// include them.
/// </summary>
public sealed class BaseControllerAjaxPrivacyTests
{
    /// <summary>
    /// When the HTTP request carries form data with PII (e.g. an email address),
    /// the AJAX failure payload returned to the client must not contain that data
    /// and DiagnosticLog must be null (property was removed from the VM).
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
        // Provide a readable Form directly via the feature
        httpContext.Request.Form = formCollection;

        var controller = CreateController(httpContext);

        // Act
        var result = controller.InvokeAjaxErrorResult("Operace selhala.", details: "test-details");

        // Assert — VM must not carry any PII from form values
        var vm = result.Value.Should().BeOfType<ModalSubmitResultViewModel>().Subject;

        // DiagnosticLog property was removed from ModalSubmitResultViewModel as part of QW-7
        // Verify the property does not exist on the type at all
        typeof(ModalSubmitResultViewModel)
            .GetProperty("DiagnosticLog")
            .Should().BeNull(
                because: "DiagnosticLog was removed from ModalSubmitResultViewModel (QW-7 PII fix)");

        // The Message should be present but must not contain PII
        vm.Message.Should().NotContain(piiEmail);
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
