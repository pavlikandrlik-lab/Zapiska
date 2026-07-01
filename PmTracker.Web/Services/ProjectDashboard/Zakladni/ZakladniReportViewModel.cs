namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public sealed record ReportSection(string Key, IReadOnlyList<ChartData> Charts);

public sealed record ZakladniReportViewModel(
    int ProjektId,
    string ProjektNazev,
    Obdobi Obdobi,
    IReadOnlyList<ReportSection> Sections);
