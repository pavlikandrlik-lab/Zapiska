namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjectDashboardPageViewModel : BaseViewModel
{
    public required ProjektHeaderViewModel Projekt { get; init; }

    /// <summary>Záložka vynucená odkazem <c>?dashTab=</c>; null = rozhodne uložený stav pm-tabs.</summary>
    public string? ActiveTab { get; set; }

    public required string RecordsPanelUrl { get; init; }
    public required string NesPanelUrl { get; init; }
    public required string StatisticsPanelUrl { get; init; }
    public required string SouhrnUrl { get; init; }
    public required string BackUrl { get; init; }

    /// <summary>Zkratka napojeného IS (chip v hlavičce); null = projekt bez napojení.</summary>
    public string? IsZkratka { get; init; }

    /// <summary>Kde se nastavuje IS projektu (editační modal v přehledu projektů) — chip „bez napojení na IS".</summary>
    public string NastaveniIsUrl { get; init; } = "/Projekty";
}

/// <summary>Stav dlaždice souhrnného pásu — určuje akcentní barvu i ikonu.</summary>
public enum SouhrnStav { Ok, Varovani, Kriticke, Nedostupne }

/// <summary>Dlaždice souhrnného pásu dashboardu (spec 2026-10-15 §8).</summary>
/// <param name="Klic">ukoly-po-terminu | ceka-na-skutecnost | tickety-v-prodleni | hrozi-prodleni</param>
/// <param name="Hodnota">Velké číslo; null = data nejsou k dispozici (zobrazí se „—").</param>
/// <param name="Doplnek">Text vedle čísla, např. „38 dní".</param>
/// <param name="CilovaZalozka">Klíč pm-tab, na který klik přepne.</param>
/// <param name="CilovyFiltr">Filtr, který se po načtení záložky nastaví.</param>
public sealed record SouhrnDlazdice(string Klic, int? Hodnota, string Popisek, string? Doplnek, SouhrnStav Stav,
    string CilovaZalozka, string? CilovyFiltr);

public sealed class DashboardSouhrnViewModel
{
    public IReadOnlyList<SouhrnDlazdice> Dlazdice { get; init; } = [];
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

    /// <summary>Počty řádků podle kategorie pro filtrační chipy (vždy všechny tři klíče, bez nového dotazu).</summary>
    public IReadOnlyDictionary<DashboardRecordCategory, int> PocetPodleKategorie =>
        Enum.GetValues<DashboardRecordCategory>().ToDictionary(k => k, k => Records.Count(r => r.Category == k));
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
