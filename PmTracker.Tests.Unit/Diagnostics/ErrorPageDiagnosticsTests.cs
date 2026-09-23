using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// Po pádu už akce běží na /Home/Error, takže naivně sestavená diagnostika by hlásila
/// cestu /Home/Error a byla by k ničemu. Původní cesta se musí brát
/// z IExceptionHandlerPathFeature, kterou naplní UseExceptionHandler.
/// </summary>
public sealed class ErrorPageDiagnosticsTests
{
    private static HomeController Controller(HttpContext context)
    {
        var resolver = new Mock<IUserContextResolver>();
        var controller = new HomeController(
            resolver.Object, TimeProvider.System, NullLoggerFactory.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        return controller;
    }

    [Fact]
    public void Error_NeseDiagnostikuSPuvodniCestouAVyjimkou()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-xyz" };
        context.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Path = "/Export/Vyzva/5/Word",
            Error = new InvalidOperationException("boom"),
        });
        context.Request.Method = "GET";

        var result = Controller(context).Error().Should().BeOfType<ViewResult>().Subject;
        var vm = result.Model.Should().BeOfType<ErrorViewModel>().Subject;

        vm.RequestId.Should().Be("trace-xyz");
        vm.ShowDiagnostics.Should().BeTrue();
        vm.DiagnosticLog.Should().Contain("/Export/Vyzva/5/Word",
            "diagnostika musí ukázat cestu, která spadla, ne /Home/Error");
        vm.DiagnosticLog.Should().Contain("InvalidOperationException").And.Contain("boom");
    }

    /// <summary>
    /// Bez výjimky (uživatel si stránku otevřel přímo) se panel nenabízí — prázdný
    /// výpis by jen mátl.
    /// </summary>
    [Fact]
    public void Error_BezVyjimky_Nediagnostikuje()
    {
        var vm = Controller(new DefaultHttpContext { TraceIdentifier = "t" })
            .Error().Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<ErrorViewModel>().Subject;

        vm.ShowDiagnostics.Should().BeFalse();
    }
}
