namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotZaznamDto(
    string Id,
    string? TypZaznamu,
    string? Strucne,
    string? Popis);
