using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.IISIntegration;
using PmTracker.ServiceDesk.Sql;
using PmTracker.Web.Extensions;
using PmTracker.Web.Filters;
using PmTracker.Web.Middleware;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;

// Aliases to resolve naming collision with Microsoft.AspNetCore.Authorization.IAuthorizationService
using PmTrackerAuthzService = PmTracker.Web.Services.Security.IAuthorizationService;
using PmTrackerAuthzServiceImpl = PmTracker.Web.Services.Security.AuthorizationService;

var builder = WebApplication.CreateBuilder(args);

// Souborový log (2026-09-08). Bez něj šlo všechno jen do konzole, kterou IIS v in-process
// režimu zahazuje, takže po chybě nezbyla žádná stopa a nešlo zjistit proč ani kde nastala.
builder.Logging.AddPmTrackerFileLog(builder.Configuration, builder.Environment);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AjaxAntiforgeryResultFilter>();
});
// Plán 4 Feature C Task 4 — auto-fill skutečnosti harmonogramu z SD bindingů.
builder.Services.AddScoped<
    PmTracker.Web.Services.Schedules.IHarmonogramSkutecnostSyncService,
    PmTracker.Web.Services.Schedules.HarmonogramSkutecnostSyncService>();
builder.Services.AddScoped<
    PmTracker.Web.Services.Records.IRecordEditLockService,
    PmTracker.Web.Services.Records.RecordEditLockService>();
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PmTrackerAuthzService, PmTrackerAuthzServiceImpl>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPermissionPolicies();
});
// M-3: Rate limiter — ochrana drahých FTS endpointů před zneužitím
// (authentikovaný user hot-loop → DoS na FREETEXTTABLE queries).
// 30 req / 10s per user; pokud uživatel překročí, vrací 429.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("search", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromSeconds(10);
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
builder.Services.Configure<PmTracker.Web.Services.Export.PdfExportOptions>(
    builder.Configuration.GetSection(PmTracker.Web.Services.Export.PdfExportOptions.SectionName));
builder.Services
    .AddPmTrackerDataStore(builder.Configuration);
builder.Services.AddPmTrackerSearch(builder.Configuration);
builder.Services.AddServiceDeskIntegration(builder.Configuration);
builder.Services.AddVyzvyServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var permissionSeeder = scope.ServiceProvider.GetRequiredService<PermissionSeeder>();
    await permissionSeeder.SeedAsync(CancellationToken.None);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Musí být UVNITŘ UseExceptionHandler (tedy registrovaný až za ním): výjimku chytí jako
// první, doplní kontext požadavku do logu a pustí ji dál, aby chybovou stránku vykreslil
// handler nad ním. Registruje se ve všech prostředích — ve vývoji je log stejně užitečný.
app.UseMiddleware<RequestExceptionLoggingMiddleware>();

// M-4: defense-in-depth security headers — CSP, X-Frame-Options, X-Content-Type-Options,
// Referrer-Policy + zachovaný X-Trace-Id pro diagnostiku.
// Intranet gov app: 'unsafe-inline' je nutné dokud neproejdeme nonces pro gov-design-system
// bootstrap skripty; 'unsafe-eval' NEPOUŽÍVÁME.
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Trace-Id"] = context.TraceIdentifier;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "same-origin";
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "base-uri 'self'";
        return Task.CompletedTask;
    });
    await next();
});

app.UseHttpsRedirection();

// FIX 2026-07-10: explicitní charset pro textové statické soubory. Bez něj posílá
// server jen `text/css` a prohlížeč kódování HÁDÁ — Edge na české Windows (i15)
// spadl na CP1250 a UTF-8 glyfy v CSS (breadcrumb separátor „›", filtry „▾")
// renderoval jako mojibake („â€ş"). HTTP hlavička má nejvyšší prioritu → konec hádání.
var staticContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticContentTypeProvider.Mappings[".css"] = "text/css; charset=utf-8";
staticContentTypeProvider.Mappings[".js"] = "text/javascript; charset=utf-8";

if (app.Environment.IsDevelopment())
{
    // Dev-only: ESM moduly se importují relativním URL bez verze (asp-append-version verzuje
    // jen entry site.js, jehož obsah se při změně submodulu nemění → prohlížeč servíruje
    // cached site.js i cached importované moduly). Při iteraci na JS to způsobuje, že změny
    // v modulech nedorazí do prohlížeče bez ručního clear-cache. no-cache vynutí revalidaci
    // (na localhostu instantní) → vývojář vždy dostane čerstvé moduly. V produkci necháváme
    // standardní caching (deploy = plná výměna souborů + ohlášený hard-refresh).
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = staticContentTypeProvider,
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || ctx.File.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            }
        }
    });
}
else
{
    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = staticContentTypeProvider
    });
}

app.UseRouting();

app.UseAuthentication();
// HIGH-1 fix: naplnit HttpContext.Items[osobaId] PŘED UseAuthorization(),
// aby PermissionAuthorizationHandler mohl číst osoba z ICurrentUserAccessor
// při vyhodnocení [Authorize(Policy = "permission:xxx")] policies.
app.UseMiddleware<UserContextMiddleware>();
app.UseAuthorization();

// M-3: Rate limiter middleware musí být PO UseAuthorization (user context známý)
// a PŘED MapControllers (aby [EnableRateLimiting] atributy byly aplikovány).
app.UseRateLimiter();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

public partial class Program;
