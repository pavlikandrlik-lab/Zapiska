namespace PmTracker.Web.Middleware;

/// <summary>
/// Zaloguje neošetřenou výjimku i s kontextem požadavku a pustí ji dál.
/// </summary>
/// <remarks>
/// Vzniklo 2026-09-08, kdy tisk výzvy vracel HTTP 500 a nešlo zjistit proč ani kde.
/// Vestavěný <c>ExceptionHandlerMiddleware</c> výjimku sice zaloguje, ale pod svou
/// vlastní kategorií a bez cesty požadavku — z takového záznamu se nedá určit, který
/// endpoint selhal a s jakými parametry. Tady se doplní metoda, cesta, dotaz, trace id
/// a uživatel, takže je v logu rovnou vidět „co se volalo, kdo to volal a co spadlo".
///
/// Výjimka se záměrně pouští dál: chybovou stránku renderuje až <c>UseExceptionHandler</c>
/// nad tímto middlewarem. Spolknutí by uživateli vrátilo prázdnou odpověď.
///
/// Trace id je stejné, jaké jde v hlavičce <c>X-Trace-Id</c> a zobrazuje se na chybové
/// stránce jako Request ID — uživatel ho tedy může nadiktovat a v logu se podle něj hledá.
/// </remarks>
public sealed class RequestExceptionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestExceptionLoggingMiddleware> _logger;

    public RequestExceptionLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestExceptionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Neošetřená výjimka: {Method} {Path}{Query} | trace {TraceId} | uživatel {User}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value,
                context.TraceIdentifier,
                context.User?.Identity?.Name ?? "(neurčen)");
            throw;
        }
    }
}
