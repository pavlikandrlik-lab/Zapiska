namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotKalkulaceDto(
    long Id,
    string Pid,
    int? Verze,
    decimal? PracnostAnalyza, decimal? SazbaAnalyza, decimal? CenaAnalyza,
    decimal? PracnostProgramovani, decimal? SazbaProgramovani, decimal? CenaProgramovani,
    decimal? PracnostTestovani, decimal? SazbaTestovani, decimal? CenaTestovani,
    decimal? PracnostImplementace, decimal? SazbaImplementace, decimal? CenaImplementace,
    decimal? CenaCelkem,
    int? PocetLicenci, decimal? SazbaLicence, decimal? CenaLicence, string? RozpadLicence,
    DateTime? Termin, string? TextTermin);
