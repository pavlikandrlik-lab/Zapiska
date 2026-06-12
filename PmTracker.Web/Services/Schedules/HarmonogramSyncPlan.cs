using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Datum-model (2026-06-12) — DTO zamýšlených změn skutečnosti pro jeden záznam.
/// Výstup <see cref="IHarmonogramSkutecnostSyncService.ComputePlanAsync"/>,
/// vstup <see cref="IHarmonogramSkutecnostSyncService.ApplyPlanAsync"/>.
/// </summary>
public sealed record HarmonogramSyncPlan(
    int ProjektovyZaznamId,
    DateTime ComputedAtUtc,
    IReadOnlyList<HarmonogramKrokChange> Changes);

/// <summary>
/// Jedna zamýšlená změna řádku <c>zaznam_harmonogram_krok</c> (skutečnost = absolutní datum).
/// NULL SkutecnostDatum = retract / krok nenastal.
/// </summary>
public sealed record HarmonogramKrokChange(
    int Poradi,
    DateTime? OldSkutecnostDatum,
    DateTime? NewSkutecnostDatum,
    SkutecnostZdrojEnum OldZdroj,
    SkutecnostZdrojEnum NewZdroj,
    int? OldPreferredExterniOdkazId,
    int? NewPreferredExterniOdkazId,
    DateTime ExpectedUpdatedAt,
    HarmonogramRowChangeReason Reason);

/// <summary>Důvod zamýšlené změny — audit / UI „co se chystá změnit".</summary>
public enum HarmonogramRowChangeReason
{
    NoChange = 0,
    NewAutomatValue,
    UpdatedAutomatValue,
    RetractAutomat_NoCandidates,
    PreferredFallback,
    SkippedManualRezim,
    CreateAutomatRow
}
