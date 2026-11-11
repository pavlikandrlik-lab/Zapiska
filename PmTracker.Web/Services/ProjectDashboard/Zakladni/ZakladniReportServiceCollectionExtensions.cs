using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Grafy reportu Statistik. Jediné místo registrace — testy z něj ověřují, že každý provider v sestavě
/// je zaregistrovaný a patří do známé sekce.
/// </summary>
public static class ZakladniReportServiceCollectionExtensions
{
    public static IServiceCollection AddZakladniReportProviders(this IServiceCollection services)
    {
        // Přehled
        services.AddScoped<IZakladniChartProvider, PrehledKpiProvider>();
        // Průběh práce
        services.AddScoped<IZakladniChartProvider, BurnUpProvider>();
        services.AddScoped<IZakladniChartProvider, RecordStatusPieProvider>();
        services.AddScoped<IZakladniChartProvider, RecordStatusBreakdownProvider>();
        // Termíny
        services.AddScoped<IZakladniChartProvider, DeadlineCompliancePerSubsystemProvider>();
        services.AddScoped<IZakladniChartProvider, DeadlineAvgShiftDaysPerSubsystemProvider>();
        services.AddScoped<IZakladniChartProvider, OpenTaskAgingProvider>();
        // Lidé a spolupráce
        services.AddScoped<IZakladniChartProvider, OwnerWorkloadProvider>();
        services.AddScoped<IZakladniChartProvider, AttendancePerMonthProvider>();
        services.AddScoped<IZakladniChartProvider, VyjadreniPerSubsystemProvider>();
        return services;
    }
}
