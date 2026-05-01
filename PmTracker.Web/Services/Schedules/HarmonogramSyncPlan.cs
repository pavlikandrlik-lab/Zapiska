using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Plán Harmonogram refactor 2026-05-01 (DESIGN-4-A) — DTO popisující zamýšlené změny
/// pro jeden záznam, výstup <see cref="IHarmonogramSkutecnostSyncService.ComputePlanAsync"/>
/// a vstup <see cref="IHarmonogramSkutecnostSyncService.ApplyPlanAsync"/>.
///
/// Use cases:
/// 1. UI staging (PreviewSync endpoint) — server vrátí plán, JS drží v sessionStorage,
///    commit na Save přes ApplyPlanAsync.
/// 2. Testovatelnost — pure compute lze testovat bez DB write.
/// 3. Race resistance — caller může mezi Compute a Apply ověřit, že rows se nezměnily
///    (UpdatedAt token check). Pokud ano, znovu Compute.
/// </summary>
public sealed record HarmonogramSyncPlan(
    int ProjektovyZaznamId,
    DateTime ComputedAtUtc,
    IReadOnlyList<HarmonogramRowChange> Changes);

/// <summary>
/// Jeden zamýšlený delta zápis na <c>zaznam_harmonogram_hodnoty</c> řádek.
/// DESIGN-10-A: Old/NewHodnotaInt nullable (NULL = retract / krok nenastal).
/// </summary>
public sealed record HarmonogramRowChange(
    int RowId,
    int TypId,
    int KrokPoradi,
    int? OldHodnotaInt,
    int? NewHodnotaInt,
    SkutecnostZdrojEnum OldZdroj,
    SkutecnostZdrojEnum NewZdroj,
    int? OldPreferredExterniOdkazId,
    int? NewPreferredExterniOdkazId,
    DateTime ExpectedUpdatedAt,
    HarmonogramRowChangeReason Reason);

/// <summary>
/// Důvod zamýšlené změny — pro audit / UI display "co se chystá změnit".
/// </summary>
public enum HarmonogramRowChangeReason
{
    NoChange = 0,
    NewAutomatValue,
    UpdatedAutomatValue,
    RetractAutomat_NoCandidates,
    PreferredFallback,
    SkippedManualRezim
}
