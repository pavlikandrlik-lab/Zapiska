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

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AjaxAntiforgeryResultFilter>();
});
builder.Services.AddScoped<PmTracker.Web.Services.Schedules.SchedulePreviewService>();
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
app.UseStaticFiles();

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
