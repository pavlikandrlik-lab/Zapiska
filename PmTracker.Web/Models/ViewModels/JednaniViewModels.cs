namespace PmTracker.Web.Models.ViewModels;

public sealed class JednaniIndexViewModel
{
    public required IReadOnlyList<JednaniProjektListItemViewModel> Projekty { get; init; }
}

public sealed class JednaniProjektListItemViewModel
{
    public int ProjektId { get; init; }
    public required string ProjektNazev { get; init; }
    public required IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; }
}

public sealed class JednaniDetailViewModel
{
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
}

public sealed class JednaniTaskItemPartialViewModel
{
    public int ProjektId { get; init; }
    public int JednaniId { get; init; }
    public int JednaniCislo { get; init; }
    public bool CanEditRecordNotes { get; init; }
    public bool CanCommentAsSubsystemLeader { get; init; }
    public required JednaniUkolViewModel Ukol { get; init; }
}
