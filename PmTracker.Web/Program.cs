using Microsoft.AspNetCore.Server.IISIntegration;
using PmTracker.ServiceDesk.Sql;
using PmTracker.Web.Filters;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AjaxAntiforgeryResultFilter>();
});
builder.Services.AddScoped<PmTracker.Web.Services.Schedules.SchedulePreviewService>();
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IApplicationVersionProvider, ApplicationVersionProvider>();
builder.Services
    .AddPmTrackerDataStore(builder.Configuration);
builder.Services.AddPmTrackerSearch(builder.Configuration);
builder.Services.AddServiceDeskIntegration(builder.Configuration);

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

public partial class Program;
