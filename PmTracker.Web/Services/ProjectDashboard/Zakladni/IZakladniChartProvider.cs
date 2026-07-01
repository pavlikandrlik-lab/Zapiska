namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Vyrábí podklady pro JEDEN graf základního reportu. Single responsibility.
/// Implementace se registrují do DI; <see cref="ZakladniReportBuilder"/> je projede.
/// </summary>
public interface IZakladniChartProvider
{
    /// <summary>Stabilní identifikátor grafu (např. „records-per-subsystem").</summary>
    string Key { get; }

    /// <summary>Do které sekce reportu graf patří.</summary>
    string SectionKey { get; }

    /// <summary>Pořadí v rámci sekce (vzestupně).</summary>
    int Order { get; }

    ChartData Build(ZakladniDataset dataset);
}
