using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektHarmonogramTabViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<ProjektHarmonogramUkolViewModel> HarmonogramUkoly { get; init; } = Array.Empty<ProjektHarmonogramUkolViewModel>();
}

public sealed class ProjektHarmonogramUkolViewModel
{
    public int ZaznamId { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public string? TypUkolu { get; init; }
    public required string Stav { get; init; }
    public required string SubsystemKod { get; init; }
    public required string Subsystem { get; init; }
    public int SubsystemPoradi { get; init; }
    public bool SubsystemHasProjectOrder { get; init; }
    public required string Vlastnik { get; init; }
    public bool Stihame { get; init; }
    public HarmonogramBlockViewModel HarmonogramBlok { get; init; } = new();
    public bool CanManageSchedule { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? ScheduleEditUrl { get; set; }
    public string? ScheduleProposalUrl { get; set; }
}

public sealed class HarmonogramBlockViewModel
{
    public int RecordId { get; init; }
    public string Mode { get; init; } = "project-readonly";
    public DateTime DatumZalozeni { get; init; }
    public DateTime TerminUkonceni { get; init; }
    public string DelayBarvaHex { get; init; } = "#dc2626";
    public HarmonogramSouhrnViewModel Souhrn { get; init; } = new();
    public IReadOnlyList<HarmonogramKrokEditViewModel> Kroky { get; init; } = Array.Empty<HarmonogramKrokEditViewModel>();
    public ScheduleEditorPermissionSet Permissions { get; set; } = ScheduleEditorPermissionSet.ForReadOnly();
    public IReadOnlyDictionary<int, string> EditorChangedTypeTooltips { get; set; } = new Dictionary<int, string>();
    public string ScheduleVersion { get; init; } = string.Empty;
}

public sealed class HarmonogramKrokEditViewModel
{
    public int KrokIndex { get; init; }
    public required string Nazev { get; init; }
    public required string BarvaHex { get; init; }
    public int TrvaniTypId { get; init; }
    public int ZpozdeniTypId { get; init; }
    public int TrvaniDni { get; init; }
    public int OdchylkaDni { get; init; }
    public int ZpozdeniDni => OdchylkaDni;
    public DateTime BaselineDatum { get; init; }
    public DateTime SkutecneDatum { get; init; }
    public DateTime PosunuteDatum => SkutecneDatum;
}

public sealed class HarmonogramSouhrnViewModel
{
    public DateTime BaselineDokonceni { get; init; }
    public DateTime SkutecneDokonceni { get; init; }
    public DateTime PosunuteDokonceni => SkutecneDokonceni;
    public DateTime TerminUkolu { get; init; }
    public int CelkoveTrvaniDni { get; init; }
    public int CelkovaOdchylkaDni { get; init; }
    public int CelkoveZpozdeniDni => CelkovaOdchylkaDni;
    public bool Stihame { get; init; }
    public int PrekroceniDni { get; init; }
}
