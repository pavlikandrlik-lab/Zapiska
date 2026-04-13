namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektyIndexViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required IReadOnlyList<ProjektListItemViewModel> Projekty { get; init; }
    public IReadOnlyList<LookupOptionViewModel> StavyProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool IsAdmin { get; init; }
    public bool CanCreate { get; init; }
}

public sealed class ProjektListItemViewModel
{
    public int Id { get; init; }
    public required string Zkratka { get; init; }
    public required string Nazev { get; init; }
    public string? StavKod { get; init; }
    public required string Stav { get; init; }
    public bool PouzivatIdentJednani { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
}

public sealed class ProjektDetailViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public string ActiveTab { get; set; } = "zaznamy";
    public required ProjektHeaderViewModel Projekt { get; init; }
    public ProjektZaznamyTabViewModel ZaznamyTab { get; init; } = new();
    public ProjektLazyTabShellViewModel HarmonogramTab { get; init; } = new() { TabKey = "harmonogram", LoadingText = "Načítání harmonogramu..." };
    public ProjektLazyTabShellViewModel JednaniTab { get; init; } = new() { TabKey = "jednani", LoadingText = "Načítání jednání..." };
    public ProjektLazyTabShellViewModel TymTab { get; init; } = new() { TabKey = "tym", LoadingText = "Načítání týmu..." };
    public ProjektHarmonogramTabViewModel? LoadedHarmonogramTab { get; set; }
    public ProjektJednaniTabViewModel? LoadedJednaniTab { get; set; }
    public ProjektTymTabViewModel? LoadedTymTab { get; set; }
    public bool CanCreateMeetings { get; set; }
    public bool CanEditMeetings { get; set; }
    public bool CanManageTeam { get; set; }
    public bool CanManageRecords { get; set; }
    public bool CanManageSchedules { get; set; }
    public int CurrentUserOsobaId { get; set; }
    public string? CreateRecordEditorUrl { get; set; }
    public string? ReturnToProjectUrl { get; set; }
    public string? ProjectPrintUrl { get; set; }
    public string? ProjectWordUrl { get; set; }
}

public sealed class ProjektHeaderViewModel
{
    public int Id { get; init; }
    public required string Nazev { get; init; }
    public required string Zkratka { get; init; }
    public required string Stav { get; init; }
    public bool PouzivatIdentJednani { get; init; }
}

public sealed class ProjektLazyTabShellViewModel
{
    public required string TabKey { get; init; }
    public string? LoadUrl { get; set; }
    public string LoadingText { get; set; } = "Načítání...";
}

public sealed class ProjektZaznamyTabViewModel
{
    public int ProjektId { get; init; }
    public bool DleSubsystemu { get; init; }
    public IReadOnlyList<ProjektZaznamGroupViewModel> SkupinyZaznamu { get; init; } = Array.Empty<ProjektZaznamGroupViewModel>();
    public IReadOnlyList<ProjektZaznamCardShellViewModel> Zaznamy { get; init; } = Array.Empty<ProjektZaznamCardShellViewModel>();
    public ProjektFiltryViewModel Filtry { get; init; } = new();
    public int CurrentUserOsobaId { get; set; }
    public bool CanManageRecords { get; set; }
    public string? CreateRecordEditorUrl { get; set; }
    public string? RefreshUrl { get; set; }
    public string? MeetingCommentStatesUrl { get; set; }
    public string? ProjectPrintUrl { get; set; }
    public string? ProjectWordUrl { get; set; }
}

public sealed class ProjektZaznamGroupViewModel
{
    public required string Nazev { get; init; }
    public string? Kod { get; init; }
    public int Poradi { get; init; }
    public bool HasProjectOrder { get; init; }
    public IReadOnlyList<ProjektZaznamCardShellViewModel> Zaznamy { get; init; } = Array.Empty<ProjektZaznamCardShellViewModel>();
}

public sealed class ProjektZaznamCardShellViewModel
{
    public required ZaznamCardSummaryViewModel Summary { get; init; }
    public string? DetailUrl { get; set; }
    public string? CommentsUrl { get; set; }
    public ZaznamCardDetailViewModel? Detail { get; set; }
    public ZaznamCommentsPanelViewModel? Comments { get; set; }
    public bool DetailLoaded { get; set; }
    public bool CommentsLoaded { get; set; }
}

public sealed class ZaznamCardSummaryViewModel
{
    public int Id { get; init; }
    public int ProjektId { get; init; }
    public int CisloZaznamu { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public required string KategorieKod { get; init; }
    public required string KategorieNazev { get; init; }
    public string? TypUkoluKod { get; init; }
    public string? TypUkolu { get; init; }
    public string? StavKod { get; init; }
    public required string Stav { get; init; }
    public required string Cil { get; init; }
    public required string AktualniVlastnik { get; init; }
    public int AktualniVlastnikId { get; init; }
    public DateTime? AktualniTermin { get; init; }
    public required string AktualniSubsystemKod { get; init; }
    public required string AktualniSubsystem { get; init; }
    public int AktualniSubsystemPoradi { get; init; }
    public bool AktualniSubsystemHasProjectOrder { get; init; }
    public IReadOnlyList<int> AktualniSubsystemLeadEquivalentOsobaIds { get; init; } = Array.Empty<int>();
    public bool IsAktivniStav { get; init; } = true;
    public bool JeUkol { get; init; }
    public IReadOnlyList<string> VyjadreniJednaniStavyKody { get; init; } = Array.Empty<string>();
    public DateTime DatumZalozeni { get; init; }
    public bool CanEditRecord { get; set; }
    public bool CanEditSchedule { get; set; }
    public bool CanAddSchedule { get; set; }
    public bool CanManageSchedule { get; set; }
    public bool CanCommentAsSubsystemLeader { get; set; }
    public bool CanAddComment { get; set; }
    public string EditButtonLabel { get; set; } = "Upravit";
    public int CurrentUserOsobaId { get; set; }
}

public sealed class ZaznamCardDetailViewModel
{
    public required string Popis { get; init; }
    public required IReadOnlyList<string> HistorieVlastniku { get; init; }
    public required string AktualniVlastnik { get; init; }
    public required IReadOnlyList<DateTime> HistorieTerminu { get; init; }
    public DateTime? AktualniTermin { get; init; }
    public required IReadOnlyList<string> HistorieSubsystemu { get; init; }
    public required string AktualniSubsystem { get; init; }
    public string? TypUkolu { get; init; }
    public required IReadOnlyList<string> HistorieTypuUkolu { get; init; }
    public required IReadOnlyList<ExterniOdkazViewModel> ExterniOdkazy { get; init; }
    public required IReadOnlyList<SpolupracovnikViewModel> Spoluprace { get; init; }
}

public sealed class ZaznamCommentsPanelViewModel
{
    public int ProjektId { get; init; }
    public int ZaznamId { get; init; }
    public int LoadedCount { get; init; }
    public int TotalCount { get; init; }
    public int LoadStep { get; init; }
    public bool CanLoadMore { get; init; }
    public bool CanLoadAll { get; init; }
    public bool IsFullyLoaded { get; init; }
    public bool CanEditRecord { get; set; }
    public bool CanCommentAsSubsystemLeader { get; set; }
    public bool CanAddComment { get; set; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<VyjadreniViewModel> Vyjadreni { get; init; } = Array.Empty<VyjadreniViewModel>();
    public IReadOnlyList<JednaniOptionViewModel> OtevrenaJednani { get; set; } = Array.Empty<JednaniOptionViewModel>();
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
    public bool EditorJeUkolKategorie { get; set; }
    public bool EditorCanEditScheduleFull { get; set; }
    public bool EditorCanEditScheduleAddOnly { get; set; }
}

public sealed class ProjektHarmonogramTabViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<ProjektHarmonogramUkolViewModel> HarmonogramUkoly { get; init; } = Array.Empty<ProjektHarmonogramUkolViewModel>();
}

public sealed class ProjektJednaniTabViewModel
{
    public int ProjektId { get; init; }
    public IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; } = Array.Empty<JednaniListItemViewModel>();
    public IReadOnlyList<JednaniYearGroupViewModel> RocniSkupiny { get; init; } = Array.Empty<JednaniYearGroupViewModel>();
    public int? PreviewRok { get; set; }
    public IReadOnlyList<LookupOptionViewModel> StavyJednani { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool CanCreateMeetings { get; set; }
    public bool CanEditMeetings { get; set; }
    public string? ReturnToProjectUrl { get; set; }
}

public sealed class ProjektTymTabViewModel
{
    public int ProjektId { get; init; }
    public IReadOnlyList<ProjectRoleGridRowViewModel> AktivniRole { get; init; } = Array.Empty<ProjectRoleGridRowViewModel>();
    public IReadOnlyList<ProjectRoleHistoryGridRowViewModel> HistorieRoli { get; init; } = Array.Empty<ProjectRoleHistoryGridRowViewModel>();
    public IReadOnlyList<ProjectTeamSubsystemRowViewModel> AktivniSubsystemyProjektu { get; init; } = Array.Empty<ProjectTeamSubsystemRowViewModel>();
    public IReadOnlyList<ProjectMemberCandidateViewModel> DostupneOsobyProRole { get; init; } = Array.Empty<ProjectMemberCandidateViewModel>();
    public IReadOnlyList<ProjectSubsystemOptionViewModel> DostupneProjektoveSubsystemy { get; init; } = Array.Empty<ProjectSubsystemOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleSubsystemu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> DostupneSubsystemy { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool CanManageTeam { get; set; }
}

public sealed class SubsystemGroupViewModel
{
    public required string Nazev { get; init; }
    public required IReadOnlyList<ZaznamCardViewModel> Zaznamy { get; init; }
}

public sealed class ZaznamCardViewModel
{
    public int Id { get; init; }
    public int HarmonogramSablonaVerze { get; init; }
    public int CisloZaznamu { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public required string KategorieKod { get; init; }
    public required string KategorieNazev { get; init; }
    public string? TypUkoluKod { get; init; }
    public string? TypUkolu { get; init; }
    public string? StavKod { get; init; }
    public required string Stav { get; init; }
    public required string Cil { get; init; }
    public required string Popis { get; init; }
    public required IReadOnlyList<string> HistorieVlastniku { get; init; }
    public required string AktualniVlastnik { get; init; }
    public required IReadOnlyList<DateTime> HistorieTerminu { get; init; }
    public DateTime? AktualniTermin { get; init; }
    public required IReadOnlyList<string> HistorieSubsystemu { get; init; }
    public required IReadOnlyList<string> HistorieTypuUkolu { get; init; }
    public required string AktualniSubsystemKod { get; init; }
    public required string AktualniSubsystem { get; init; }
    public int AktualniSubsystemPoradi { get; init; }
    public bool AktualniSubsystemHasProjectOrder { get; init; }
    public required IReadOnlyList<int> AktualniSubsystemLeadEquivalentOsobaIds { get; init; }
    public required IReadOnlyList<ExterniOdkazViewModel> ExterniOdkazy { get; init; }
    public required IReadOnlyList<SpolupracovnikViewModel> Spoluprace { get; init; }
    public required IReadOnlyList<VyjadreniViewModel> Vyjadreni { get; init; }
    public int AktualniVlastnikId { get; init; }
    public bool IsAktivniStav { get; init; } = true;
    public bool JeUkol { get; init; }
    public required IReadOnlyList<string> VyjadreniJednaniStavyKody { get; init; }
    public bool MaVyjadreniProPripravuJednani { get; init; }
    public DateTime DatumZalozeni { get; init; }
    public bool CanEditRecord { get; set; }
    public bool CanEditSchedule { get; set; }
    public bool CanAddSchedule { get; set; }
    public bool CanManageSchedule { get; set; }
    public bool CanCommentAsSubsystemLeader { get; set; }
    public bool CanAddComment { get; set; }
    public string EditButtonLabel { get; set; } = "Upravit";
    public int CurrentUserOsobaId { get; set; }
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
    public string? ScheduleEditUrl { get; set; }
}

public sealed class ExterniOdkazViewModel
{
    public required string Typ { get; init; }
    public string? TypNazev { get; init; }
    public required string Cislo { get; init; }
    public decimal? PredpokladanaCena { get; init; }
    public string? ServiceDeskTicketId { get; init; }
    public string? ServiceDeskUrl { get; init; }
    public string? Vyzva { get; init; }
    public DateTime? DatumObjednani { get; init; }
    public DateTime? DatumPlanDodani { get; init; }
    public DateTime? DatumDodani { get; init; }
    public DateTime? DatumPrevzeti { get; init; }
}

public sealed class VyjadreniViewModel
{
    public int Id { get; init; }
    public int AutorOsobaId { get; init; }
    public required string Autor { get; init; }
    public required DateTime Datum { get; init; }
    public required string Text { get; init; }
    public int? JednaniCislo { get; init; }
    public DateTime? JednaniDatum { get; init; }
    public bool LzeUpravit { get; init; }
    public bool CanEditOwnAsSubsystemLeader { get; init; }
}

public sealed class ProjectMemberCandidateViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class ProjectRoleAssignmentViewModel
{
    public int ProjektRoleId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectRoleGridRowViewModel
{
    public int AssignmentId { get; init; }
    public required string AssignmentKind { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public required string RoleTypeLabel { get; init; }
    public string? SubsystemKod { get; init; }
    public string? SubsystemNazev { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectRoleHistoryGridRowViewModel
{
    public int AssignmentId { get; init; }
    public required string AssignmentKind { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public required string RoleTypeLabel { get; init; }
    public string? SubsystemKod { get; init; }
    public string? SubsystemNazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public DateTime? DatumOdebrani { get; init; }
}

public sealed class ProjectRoleHistoryItemViewModel
{
    public int ProjektRoleId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public DateTime? DatumOdebrani { get; init; }
}

public sealed class ProjectSubsystemViewModel
{
    public int ProjektSubsystemId { get; init; }
    public int SubsystemId { get; init; }
    public int Poradi { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectTeamSubsystemRowViewModel
{
    public int ProjektSubsystemId { get; init; }
    public int SubsystemId { get; init; }
    public int Poradi { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public bool CanMoveUp { get; init; }
    public bool CanMoveDown { get; init; }
}

public sealed class ProjectSubsystemOptionViewModel
{
    public int ProjektSubsystemId { get; init; }
    public int SubsystemId { get; init; }
    public required string Label { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
}

public sealed class ProjectSubsystemRoleAssignmentViewModel
{
    public int ProjektSubsystemRoleId { get; init; }
    public int ProjektSubsystemId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string SubsystemKod { get; init; }
    public required string SubsystemNazev { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectSubsystemRoleHistoryItemViewModel
{
    public int ProjektSubsystemRoleId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string SubsystemKod { get; init; }
    public required string SubsystemNazev { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public DateTime? DatumOdebrani { get; init; }
}

public sealed class TeamMemberViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string Role { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class TeamCandidateViewModel
{
    public int Id { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class JednaniListItemViewModel
{
    public int Id { get; init; }
    public int CisloJednani { get; init; }
    public DateTime Datum { get; init; }
    public TimeOnly CasZacatek { get; init; }
    public required string Misto { get; init; }
    public string? StavKod { get; init; }
    public required string Stav { get; init; }
    public string? UzamklOsoba { get; init; }
}

public sealed class JednaniOptionViewModel
{
    public int Id { get; init; }
    public required string Label { get; init; }
    public DateTime Datum { get; init; }
    public string? StavKod { get; init; }

    public bool IsDraft => string.Equals(StavKod, "DRAFT", StringComparison.OrdinalIgnoreCase);
}

public sealed class ZaznamEditViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackLabel { get; set; }
    public int Id { get; init; }
    public int CisloZaznamu { get; init; }
    public required string CisloViditelne { get; init; }
    public int ProjektId { get; init; }
    public bool IsCreate { get; init; }
    public bool PouzivatIdentJednani { get; init; }
    public bool MaDostupneJednaniProCislo { get; init; }
    public bool MuzeDoplnitIdentifikatorJednani { get; init; }
    public int? JednaniIdProCislo { get; init; }
    public required IReadOnlyList<JednaniOptionViewModel> JednaniProCisloOptions { get; init; }
    public required string Nazev { get; init; }
    public required string Cil { get; init; }
    public required string Kategorie { get; init; }
    public required string Popis { get; init; }
    public string? TypUkolu { get; init; }
    public required string Stav { get; init; }
    public required IReadOnlyList<string> KategorieZaznamu { get; init; }
    public required IReadOnlyList<string> StavyUkolu { get; init; }
    public required IReadOnlyList<string> TypyUkolu { get; init; }
    public DateTime DatumZalozeni { get; init; }
    public DateTime? TerminUkonceni { get; init; }
    public required IReadOnlyList<SubsystemOptionViewModel> Subsystemy { get; init; }
    public required string Subsystem { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> Vlastnici { get; init; }
    public int VlastnikId { get; init; }
    public bool JeUkolKategorie { get; init; }
    public HarmonogramBlockViewModel HarmonogramBlok { get; init; } = new();
    public required IReadOnlyList<SpolupracovnikOptionViewModel> DostupniVlastnici { get; init; }
    public required IReadOnlyList<SpolupracovnikOptionViewModel> DostupniSpolupracovnici { get; init; }
    public required IReadOnlyList<int> VybraniSpolupracovniciIds { get; init; }
    public required IReadOnlyList<ExterniOdkazEditViewModel> ExterniVazby { get; init; }
    public required IReadOnlyList<string> TypyExternichOdkazu { get; init; }
    public required IReadOnlyList<string> Vyzvy { get; init; }
    public string UiContext { get; set; } = "project";
    public int? MeetingId { get; set; }
    public string Presentation { get; set; } = "modal";
    public string? ReturnUrl { get; set; }
    public string BackUrl { get; set; } = string.Empty;
    public string ActiveEditorTab { get; set; } = "basic";
    public bool UseAjaxSubmit { get; set; } = true;
    public bool CanEditRecord { get; set; }
    public bool CanEditScheduleFull { get; set; }
    public bool CanEditScheduleAddOnly { get; set; }
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

public sealed class SubsystemOptionViewModel
{
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public int DefaultOwnerOsobaId { get; init; }
}

public sealed class ProjektFiltryViewModel
{
    public IReadOnlyList<string> Subsystemy { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> Kategorie { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> KategorieMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> StavyUkolu { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> StavyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> TypyUkolu { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> TypyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<string> Vlastnici { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LookupOptionViewModel> VlastniciMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyJednaniVyjadreni { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class ProjektMeetingCommentStatesResponseViewModel
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> StatesByRecordId { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

public sealed class ExterniOdkazEditViewModel
{
    public int Id { get; set; }
    public string? Typ { get; set; }
    public string? Cislo { get; set; }
    public decimal? PredpokladanaCena { get; set; }
    public string? Vyzva { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
}

public sealed class SpolupracovnikViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class SpolupracovnikOptionViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}
