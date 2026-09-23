namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotZaznamDto(
    string Id,
    string? TypZaznamu,
    string? Strucne,
    string? Popis,
    string? Uzivatel = null,
    string? Subsystem = null,
    string? Modul = null,
    // GINIS PID (např. A490P00ECYL1) — klíč do HOT_KALKULACE. Šestimístné číslo záznamu
    // kalkulace nespojí, ty jedou přes pid. Poziční parametr proto až na konci s výchozí
    // hodnotou, aby nerozbil existující volání.
    string? Pid = null);
