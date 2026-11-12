namespace PmTracker.Web.Models.ViewModels;

public sealed class JednaniIndexViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public required IReadOnlyList<JednaniProjektListItemViewModel> Projekty { get; init; }
}

public sealed class JednaniProjektListItemViewModel
{
    public int ProjektId { get; init; }
    public required string ProjektNazev { get; init; }
    public string ProjektZkratka { get; init; } = string.Empty;
    public string? ProjektStavKod { get; init; }
    public string? ProjektStav { get; init; }
    public string? MistoPlneni { get; init; }
    public int PocetLetos { get; init; }
    public int PocetCelkem { get; init; }
    public required IReadOnlyList<JednaniListItemViewModel> Jednani { get; init; }
    public IReadOnlyList<JednaniYearGroupViewModel> RocniSkupiny { get; init; } = Array.Empty<JednaniYearGroupViewModel>();
    public int? PreviewRok { get; set; }
    public bool CanDeleteMeetings { get; set; }
    public bool CanCreateMeetings { get; set; }
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
    public int ProjektId { get; init; }
    public required string ProjektNazev { get; init; }
    public required string ProjektZkratka { get; init; }
    public required JednaniListItemViewModel Jednani { get; init; }
    public string? OtevrenyStavKod { get; init; }
    public string? UzavrenyStavKod { get; init; }
    public required IReadOnlyList<UcastViewModel> Ucast { get; init; }
    public required IReadOnlyList<MeetingParticipantCandidateViewModel> AvailableParticipantCandidates { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> StavyJednani { get; init; }
    public required IReadOnlyList<LookupOptionViewModel> StavyUcasti { get; init; }
    public bool CanEditMeeting { get; set; }
    public bool CanEditRecords { get; set; }
    public bool HasSubsystemLeadPermission { get; set; }
    public int CurrentUserOsobaId { get; set; }

    /// <summary>Uzavřené jednání (MeetingStatePolicy.IsReadOnly) — detail ukáže jen pruh se stavem a zápis.</summary>
    public bool IsReadOnly { get; init; }

    /// <summary>Záložka Záznamy v kontextu jednání (jen otevřené / příprava).</summary>
    public ProjektZaznamyTabViewModel? ZaznamyTab { get; set; }

    /// <summary>HTML zápis jednání (šablona tisku) pro iframe srcdoc — jen uzavřené jednání a export.pdf.jednani.</summary>
    public string? ZapisHtml { get; set; }
}

public sealed class UcastViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? StavUcastiKod { get; init; }
    public required string StavUcasti { get; init; }
    /// <summary>A1 (2026-07-08): aktivní role osoby v projektu (projektové + „Role (SUBSYSTÉM)").</summary>
    public IReadOnlyList<string> AktivniRole { get; init; } = [];
}

public sealed class MeetingParticipantCandidateViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required IReadOnlyList<string> AktivniRole { get; init; }
}

public sealed class MeetingYearGroupPartialViewModel
{
    public JednaniYearGroupViewModel Rok { get; init; } = null!;
    public bool IsPreviewYear { get; init; }
    public int ProjektId { get; init; }
    public bool CanDeleteMeetings { get; init; }
}
