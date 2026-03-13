using Microsoft.AspNetCore.Server.IISIntegration;
using PmTracker.Web.Filters;
using PmTracker.Web.Middleware;
using PmTracker.Web.Modules;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AjaxAntiforgeryResultFilter>();
});
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
builder.Services
    .AddPmTrackerDataStore(builder.Configuration)
    .AddPmTrackerModules();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
        return Task.CompletedTask;
    });
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<AjaxResponseContractGuardMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Projekty}/{action=Index}/{id?}");

app.Run();

public partial class Program;
