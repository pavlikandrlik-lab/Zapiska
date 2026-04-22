namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotVyjadreniDto(
    long Id,
    string? Typ,
    string Pid,
    DateTime Datum,
    string? Zpracoval,
    string? Popis,
    string? Tym,
    int? ViditelneDodavateli);
