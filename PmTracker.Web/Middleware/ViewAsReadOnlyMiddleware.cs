using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Web.Middleware;

/// <summary>
/// Režim „Zobrazit jako“ je jen pro čtení. Běží za UserContextMiddleware (ten stav aktivuje)
/// a před MVC, takže pokryje i controllery mimo BaseController. Vedle toho převádí výjimku
/// EF pojistky (zápis při GET) na stejnou srozumitelnou odpověď.
/// </summary>
public sealed class ViewAsReadOnlyMiddleware(RequestDelegate next, ILogger<ViewAsReadOnlyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ViewAsRequestState state)
    {
        if (!state.IsActive)
        {
            await next(context);
            return;
        }

        var metadata = context.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<ViewAsControlAttribute>() is not null)
        {
            state.PermitWrites();
            await next(context);
            return;
        }

        if (!IsSafeMethod(context.Request.Method) && metadata?.GetMetadata<AllowDuringViewAsAttribute>() is null)
        {
            logger.LogInformation(
                "Pohled jako: odmítnut {Method} {Path}. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
                context.Request.Method,
                context.Request.Path.Value,
                state.RealOsobaId,
                state.TargetOsobaId);
            await WriteBlockedAsync(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (Exception ex) when (ViewAsWriteBlockedException.IsInChain(ex) && !context.Response.HasStarted)
        {
            context.Response.Clear();
            await WriteBlockedAsync(context);
        }
    }

    private static bool IsSafeMethod(string method)
        => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static async Task WriteBlockedAsync(HttpContext context)
    {
        var request = context.Request;
        if (WantsJson(request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ModalSubmitResultViewModel
            {
                Ok = false,
                Message = ViewAsMessages.ReadOnly,
                ErrorCode = AjaxErrorCodes.ViewAsReadOnly,
                TraceId = context.TraceIdentifier
            });
            return;
        }

        if (!IsSafeMethod(request.Method))
        {
            // Klasický formulář: zpět na stránku, hlášku ukáže layout (TempData["ErrorMessage"]).
            var tempData = context.RequestServices
                .GetRequiredService<ITempDataDictionaryFactory>()
                .GetTempData(context);
            tempData["ErrorMessage"] = ViewAsMessages.ReadOnly;
            tempData.Save();
            context.Response.StatusCode = StatusCodes.Status303SeeOther;
            context.Response.Headers.Location = LocalReferer.Resolve(request) ?? "/";
            return;
        }

        // Zápis při GET bez AJAXu: vědomě jen prostý text — jde o pojistku pro nečekaný případ.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync($"{ViewAsMessages.ReadOnly} ({AjaxErrorCodes.ViewAsReadOnly})");
    }

    private static bool WantsJson(HttpRequest request)
        => string.Equals(request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
           || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
           || (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);
}
