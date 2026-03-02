namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektyIndexViewModel
{
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

public sealed class ProjektDetailViewModel
{
    public required ProjektHeaderViewModel Projekt { get; init; }
    public bool DleSubsystemu { get; init; }
    public required IReadOnlyList<SubsystemGroupViewModel> SkupinySubsystemu { get; init; }
    public required IReadOnlyList<ZaznamCardViewModel> Zaznamy { get; init; }
    public required IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; }
    public required IReadOnlyList<ProjectRoleAssignmentViewModel> AktivniProjektoveRole { get; init; }
    public required IReadOnlyList<ProjectRoleHistoryItemViewModel> HistorieProjektovychRoli { get; init; }
    public required IReadOnlyList<ProjectSubsystemViewModel> AktivniSubsystemyProjektu { get; init; }
    public required IReadOnlyList<ProjectSubsystemRoleAssignmentViewModel> AktivniSubsystemoveRole { get; init; }
    public required IReadOnlyList<ProjectSubsystemRoleHistoryItemViewModel> HistorieSubsystemovychRoli { get; init; }
    public required IReadOnlyList<ProjectMemberCandidateViewModel> DostupneOsobyProRole { get; init; }
    public required IReadOnlyList<ProjectSubsystemOptionViewModel> DostupneProjektoveSubsystemy { get; init; }
    public required IReadOnlyList<ProjektHarmonogramUkolViewModel> HarmonogramUkoly { get; init; }
    public IReadOnlyList<LookupOptionViewModel> RoleProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleSubsystemu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> DostupneSubsystemy { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required IReadOnlyList<JednaniOptionViewModel> OtevrenaJednani { get; init; }
    public IReadOnlyList<LookupOptionViewModel> StavyJednani { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required ProjektFiltryViewModel Filtry { get; init; }
}

public sealed class ProjektHeaderViewModel
{
    public int Id { get; init; }
    public required string Nazev { get; init; }
    public required string Zkratka { get; init; }
    public required string Stav { get; init; }
    public bool PouzivatIdentJednani { get; init; }
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
    public required string Popis { get; init; }
    public required IReadOnlyList<string> HistorieVlastniku { get; init; }
    public required string AktualniVlastnik { get; init; }
    public required IReadOnlyList<DateTime> HistorieTerminu { get; init; }
    public DateTime? AktualniTermin { get; init; }
    public required IReadOnlyList<string> HistorieSubsystemu { get; init; }
    public required string AktualniSubsystemKod { get; init; }
    public required string AktualniSubsystem { get; init; }
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
}

public sealed class ProjektHarmonogramUkolViewModel
{
    public int ZaznamId { get; init; }
    public int CisloZaznamu { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public required string KategorieKod { get; init; }
    public required string KategorieNazev { get; init; }
    public string? TypUkoluKod { get; init; }
    public string? TypUkolu { get; init; }
    public required string Stav { get; init; }
    public string? StavKod { get; init; }
    public required string SubsystemKod { get; init; }
    public required string Subsystem { get; init; }
    public required string Vlastnik { get; init; }
    public int VlastnikId { get; init; }
    public bool IsAktivniStav { get; init; }
    public DateTime DatumZalozeni { get; init; }
    public DateTime TerminUkonceni { get; init; }
    public DateTime BaselineDokonceni { get; init; }
    public DateTime PosunuteDokonceni { get; init; }
    public int CelkoveTrvaniDni { get; init; }
    public int CelkoveZpozdeniDni { get; init; }
    public int DelkaDoTerminuDni { get; init; }
    public bool Stihame { get; init; }
    public int PrekroceniDni { get; init; }
    public required string DelayBarvaHex { get; init; }
    public required IReadOnlyList<ProjektHarmonogramKrokViewModel> Kroky { get; init; }
}

public sealed class ProjektHarmonogramKrokViewModel
{
    public int KrokIndex { get; init; }
    public required string Nazev { get; init; }
    public int TrvaniDni { get; init; }
    public int ZpozdeniDni { get; init; }
    public required string BarvaHex { get; init; }
    public DateTime PlanStart { get; init; }
    public DateTime PlanEnd { get; init; }
    public DateTime RealStart { get; init; }
    public DateTime RealEnd { get; init; }
}

public sealed class ExterniOdkazViewModel
{
    public required string Typ { get; init; }
    public string? TypNazev { get; init; }
    public required string Cislo { get; init; }
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
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
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
}

public sealed class ZaznamEditViewModel
{
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
    public required string HarmonogramDelayBarvaHex { get; init; }
    public required IReadOnlyList<HarmonogramKrokEditViewModel> HarmonogramKroky { get; init; }
    public required HarmonogramSouhrnViewModel HarmonogramSouhrn { get; init; }
    public required IReadOnlyList<SpolupracovnikOptionViewModel> DostupniSpolupracovnici { get; init; }
    public required IReadOnlyList<int> VybraniSpolupracovniciIds { get; init; }
    public required IReadOnlyList<ExterniOdkazEditViewModel> ExterniVazby { get; init; }
    public required IReadOnlyList<string> TypyExternichOdkazu { get; init; }
    public required IReadOnlyList<string> Vyzvy { get; init; }
}

public sealed class HarmonogramKrokEditViewModel
{
    public int KrokIndex { get; init; }
    public required string Nazev { get; init; }
    public required string BarvaHex { get; init; }
    public int TrvaniTypId { get; init; }
    public int ZpozdeniTypId { get; init; }
    public int TrvaniDni { get; init; }
    public int ZpozdeniDni { get; init; }
    public DateTime BaselineDatum { get; init; }
    public DateTime PosunuteDatum { get; init; }
}

public sealed class HarmonogramSouhrnViewModel
{
    public DateTime BaselineDokonceni { get; init; }
    public DateTime PosunuteDokonceni { get; init; }
    public DateTime TerminUkolu { get; init; }
    public int CelkoveTrvaniDni { get; init; }
    public int CelkoveZpozdeniDni { get; init; }
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
    public required IReadOnlyList<string> Subsystemy { get; init; }
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required IReadOnlyList<string> Kategorie { get; init; }
    public IReadOnlyList<LookupOptionViewModel> KategorieMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required IReadOnlyList<string> StavyUkolu { get; init; }
    public IReadOnlyList<LookupOptionViewModel> StavyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required IReadOnlyList<string> TypyUkolu { get; init; }
    public IReadOnlyList<LookupOptionViewModel> TypyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required IReadOnlyList<string> Vlastnici { get; init; }
    public IReadOnlyList<LookupOptionViewModel> VlastniciMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyJednaniVyjadreni { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class ExterniOdkazEditViewModel
{
    public int Id { get; set; }
    public string? Typ { get; set; }
    public string? Cislo { get; set; }
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
