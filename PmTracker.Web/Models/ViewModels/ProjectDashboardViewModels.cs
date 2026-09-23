namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjectDashboardPageViewModel : BaseViewModel
{
    public required ProjektHeaderViewModel Projekt { get; init; }
    public string ActiveTab { get; set; } = "zaznamy";
    public required string RecordsPanelUrl { get; init; }
    public required string NesPanelUrl { get; init; }
    public required string StatisticsPanelUrl { get; init; }
    public required string BackUrl { get; init; }
}

public enum DashboardRecordCategory
{
    Delayed,
    AwaitingActual,
    ApproachingDeadline
}

public sealed class ProjectDashboardRecordsPanelViewModel
{
    public IReadOnlyList<ProjectDashboardRecordRowViewModel> Records { get; init; } = [];
    public bool IsEmpty => Records.Count == 0;
}

public sealed class ProjectDashboardRecordRowViewModel
{
    public int RecordId { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public required string Subsystem { get; init; }
    public required string SubsystemKod { get; init; }
    public required string Vlastnik { get; init; }
    public DateTime TerminUkonceni { get; init; }
    public DashboardRecordCategory Category { get; init; }
    public int WorstOffsetDays { get; init; }
    public IReadOnlyList<ProjectDashboardStepDetailViewModel> ProblematicSteps { get; init; } = [];
    public required string ScheduleUrl { get; init; }
}

public sealed class ProjectDashboardStepDetailViewModel
{
    public required string StepName { get; init; }
    public DateTime PlannedDate { get; init; }
    public DateTime? ActualDate { get; init; }
    public int OffsetDays { get; init; }
    public int DaysSincePlanExpired { get; init; }
}

public sealed class ProjectDashboardNesPanelViewModel
{
    public bool IsServiceDeskIntegrated { get; init; }
    public int? ServiceDeskInfoSystemId { get; init; }
    public string? IsZkratka { get; init; }
    public IReadOnlyList<NesPanelItemViewModel> Items { get; init; } = Array.Empty<NesPanelItemViewModel>();
    public int PocetVProdleni { get; init; }
    public double PrumerneProdleniDni { get; init; }
    public string PlaceholderMessage { get; init; } = "Projekt není napojen na žádný Informační systém v ServiceDesku.";
}

public sealed class NesPanelItemViewModel
{
    public required int TicketId { get; init; }
    public required string Pid { get; init; }
    public required string TypZaznamu { get; init; }
    public string? Strucne { get; init; }
    public string? Dodavatel { get; init; }
    public required DateTime Termin { get; init; }
    public required int DniProdleni { get; init; }
    public string? Stav { get; init; }
    public string? ServiceDeskUrl { get; init; }
}

public sealed class ProjectDashboardStatisticsPanelViewModel
{
    public int SelectedYear { get; init; }
    public IReadOnlyList<int> AvailableYears { get; init; } = [];
    public ProjectDashboardKpiViewModel Kpi { get; init; } = new();
    public IReadOnlyList<ProjectDashboardQuarterViewModel> Quarters { get; init; } = [];
    public IReadOnlyList<ProjectDashboardSubsystemStatsViewModel> SubsystemStats { get; init; } = [];
    public double? AttendanceRate { get; init; }
    public bool IsServiceDeskIntegrated { get; init; }
    public bool HasData => Kpi.Splneno > 0 || Kpi.Zruseno > 0 || Kpi.Preneseno > 0 || Kpi.VProdleni > 0;
}

public sealed class ProjectDashboardKpiViewModel
{
    public int Splneno { get; init; }
    public int Zruseno { get; init; }
    public int Preneseno { get; init; }
    public double VcasnostPlneniPct { get; init; }
    public int VProdleni { get; init; }
    public double PrumerneProdleniDni { get; init; }
    public int Prodlouzeno { get; init; }

    public int? SplnenoPrevYear { get; init; }
    public int? ZrusenoPrevYear { get; init; }
    public int? PrenesenoPrevYear { get; init; }
    public double? VcasnostPlneniPctPrevYear { get; init; }
    public int? VProdleniPrevYear { get; init; }
    public double? PrumerneProdleniDniPrevYear { get; init; }
    public int? ProdlouzenoPrevYear { get; init; }
}

public sealed class ProjectDashboardQuarterViewModel
{
    public int Quarter { get; init; }
    public int PlannedCompletions { get; init; }
    public int ActualCompletions { get; init; }
}

public sealed class ProjectDashboardSubsystemStatsViewModel
{
    public required string SubsystemKod { get; init; }
    public required string SubsystemNazev { get; init; }
    public int Splneno { get; init; }
    public int VProdleni { get; init; }
    public int Zruseno { get; init; }
    public int Prodlouzeno { get; init; }
}
