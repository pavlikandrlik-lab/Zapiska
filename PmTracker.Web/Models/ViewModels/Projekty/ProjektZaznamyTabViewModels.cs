namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektZaznamyTabViewModel
{
    public int ProjektId { get; init; }
    public bool DleSubsystemu { get; init; }
    public IReadOnlyList<ProjektZaznamGroupViewModel> SkupinyZaznamu { get; init; } = Array.Empty<ProjektZaznamGroupViewModel>();
    public IReadOnlyList<ProjektZaznamCardShellViewModel> Zaznamy { get; init; } = Array.Empty<ProjektZaznamCardShellViewModel>();

    /// <summary>
    /// Sdílený filter shell — identický s <see cref="ProjektHarmonogramTabViewModel.FilterShell"/>.
    /// Render přes <c>_ProjectFilterShell.cshtml</c>. Spec 2026-04-30-project-filter-unification-design.
    /// </summary>
    public ProjectFilterShellViewModel FilterShell { get; init; } = new();
    public int CurrentUserOsobaId { get; set; }
    public bool CanManageRecords { get; set; }
    public bool CanCreateRecordProposal { get; set; }
    public string? CreateRecordEditorUrl { get; set; }
    public string? CreateRecordProposalUrl { get; set; }
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
    /// <summary>
    /// Záznam má aspoň jednu vyplněnou hodnotu harmonogramu (plán NEBO skutečnost) → existuje
    /// pro něj dlaždice v projektové záložce harmonogram, takže smí mít tlačítko překliku.
    /// Sdílený predikát viz <see cref="PmTracker.Web.Services.Schedules.HarmonogramKrokPredicates"/>.
    /// </summary>
    public bool MaHarmonogramHodnotu { get; init; }
    public IReadOnlyList<string> VyjadreniJednaniStavyKody { get; init; } = Array.Empty<string>();
    public DateTime DatumZalozeni { get; init; }
    public bool CanEditRecord { get; set; }
    public bool CanEditSchedule { get; set; }
    // F4 redesign 2026-04-23: CanAddSchedule smazáno — records.schedule.add obsolete,
    // uživatelé bez RecordsScheduleEdit musí přes ProposalsScheduleCreate (viz CanCreateScheduleProposal).
    public bool CanManageSchedule { get; set; }
    public bool CanCommentAsSubsystemLeader { get; set; }
    public bool CanAddComment { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string EditButtonLabel { get; set; } = "Upravit";
    public string? ScheduleProposalUrl { get; set; }
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

public sealed class SubsystemGroupViewModel
{
    public required string Nazev { get; init; }
    public required IReadOnlyList<ZaznamCardViewModel> Zaznamy { get; init; }
}

public sealed class ZaznamCardViewModel
{
    public int Id { get; init; }
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
    // F4 redesign 2026-04-23: CanAddSchedule smazáno (viz ZaznamCardSummaryViewModel).
    public bool CanManageSchedule { get; set; }
    public bool CanCommentAsSubsystemLeader { get; set; }
    public bool CanAddComment { get; set; }
    public string EditButtonLabel { get; set; } = "Upravit";
    public int CurrentUserOsobaId { get; set; }
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

public sealed class ProjektMeetingCommentStatesResponseViewModel
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> StatesByRecordId { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}
