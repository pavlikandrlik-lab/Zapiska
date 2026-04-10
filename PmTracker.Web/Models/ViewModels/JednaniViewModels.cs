namespace PmTracker.Web.Models.ViewModels;

public sealed class JednaniIndexViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public required IReadOnlyList<JednaniProjektListItemViewModel> Projekty { get; init; }
}

public sealed class JednaniProjektListItemViewModel
{
    public int ProjektId { get; init; }
    public required string ProjektNazev { get; init; }
    public required IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; }
    public IReadOnlyList<JednaniYearGroupViewModel> RocniSkupiny { get; init; } = Array.Empty<JednaniYearGroupViewModel>();
    public int? PreviewRok { get; set; }
    public bool CanDeleteMeetings { get; set; }
}

public sealed class JednaniYearGroupViewModel
{
    public int Rok { get; init; }
    public IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; } = Array.Empty<JednaniListItemViewModel>();
    public int PocetJednani => Jednani.Count;
}

public sealed class JednaniDetailViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public int ProjektId { get; init; }
    public required string ProjektNazev { get; init; }
    public required JednaniListItemViewModel Jednani { get; init; }
    public string? OtevrenyStavKod { get; init; }
    public string? UzavrenyStavKod { get; init; }
    public required IReadOnlyList<UcastViewModel> Ucast { get; init; }
    public required IReadOnlyList<JednaniUkolViewModel> Ukoly { get; init; }
    public required IReadOnlyList<MeetingParticipantCandidateViewModel> AvailableParticipantCandidates { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> StavyJednani { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> StavyUcasti { get; init; }
    public bool CanEditMeeting { get; set; }
    public bool CanEditRecords { get; set; }
    public bool HasSubsystemLeadPermission { get; set; }
    public int CurrentUserOsobaId { get; set; }
}

public sealed class UcastViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? StavUcastiKod { get; init; }
    public required string StavUcasti { get; init; }
}

public sealed class JednaniUkolViewModel
{
    public int ZaznamId { get; init; }
    public int CisloZaznamu { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Popis { get; init; }
    public string? Zapis { get; init; }
    public bool LzeUpravovatVyjadreni { get; init; }
    public required IReadOnlyList<int> SubsystemLeadEquivalentOsobaIds { get; init; }
    public required IReadOnlyList<JednaniVyjadreniViewModel> Vyjadreni { get; init; }
}

public sealed class MeetingParticipantCandidateViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required IReadOnlyList<string> AktivniRole { get; init; }
}

public sealed class JednaniVyjadreniViewModel
{
    public int Id { get; init; }
    public int AutorOsobaId { get; init; }
    public required string Autor { get; init; }
    public DateTime Datum { get; init; }
    public required string Text { get; init; }
    public bool LzeUpravit { get; init; }
    public bool CanEditOwnAsSubsystemLeader { get; init; }
}

public sealed class JednaniTaskItemPartialViewModel
{
    public int ProjektId { get; init; }
    public int JednaniId { get; init; }
    public int JednaniCislo { get; init; }
    public bool CanEditRecordNotes { get; init; }
    public bool CanCommentAsSubsystemLeader { get; init; }
    public int CurrentUserOsobaId { get; init; }
    public required JednaniUkolViewModel Ukol { get; init; }
}
