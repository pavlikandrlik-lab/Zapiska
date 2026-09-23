using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PmTracker.Web.Services.Export;

public interface IViewRenderer
{
    Task<string> RenderToStringAsync(ControllerContext context, string viewPath, object model);
}

/// <summary>
/// Vyrenderuje Razor šablonu do řetězce, aby ji šlo předat sazbě PDF.
/// Serverové PDF (2026-09-04), spec §6.
/// </summary>
public sealed class RazorViewRenderer : IViewRenderer
{
    private readonly ICompositeViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;

    public RazorViewRenderer(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
    }

    public async Task<string> RenderToStringAsync(ControllerContext context, string viewPath, object model)
    {
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"Tiskovou šablonu {viewPath} se nepodařilo najít.");
        }

        await using var writer = new StringWriter();

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(context.HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(
            context, viewResult.View, viewData, tempData, writer, new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
