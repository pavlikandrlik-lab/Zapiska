using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services;

/// <summary>
/// Scoped cache pro číselník / lookup tabulky v rámci jednoho HTTP requestu.
/// Brání redundantnímu <c>SELECT * FROM ciselnik_*</c> dotazu, když víc composition
/// metod sáhne na stejná referenční data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Proč:</b> Detail projektu (Projekty/Detail) vyvolává ~20 plně-tabulkových
/// ToListAsync na číselníky (RecordCards, TeamComposition, LazyQueries, RecordEditorComposition
/// každá sáhne na CiselnikKategoriiZaznamu, CiselnikStavuUkolu, atd.). Každý dotaz
/// je sice rychlý (tabulky mají desítky řádků), ale round-trip latence × N = 200-400 ms
/// zbytečné doby odezvy.
/// </para>
/// <para>
/// <b>Scope: Scoped (per HTTP request).</b> Data jsou v cache pouze po dobu jednoho
/// requestu — stale read se nemůže prosadit mezi requesty. Uvnitř requestu jsou data
/// snapshotted při prvním přístupu.
/// </para>
/// <para>
/// <b>Lookup tabulky jsou seed-driven a stabilní</b> (mění se při deploy + ciselnik edit).
/// V kombinaci se seed-only authz pattern (žádné mutace za běhu requestu), je
/// bezpečné používat první snapshot po celou dobu requestu.
/// </para>
/// </remarks>
public interface ILookupTableCache
{
    Task<IReadOnlyDictionary<int, CiselnikKategoriiZaznamuEntity>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikStavuUkoluEntity>> GetTaskStatesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikTypuUkoluEntity>> GetTaskTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikStavuJednaniEntity>> GetMeetingStatesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikTypuExternichOdkazuEntity>> GetExternalLinkTypesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, VyzvaEntity>> GetVyzvyAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, SubsystemEntity>> GetSubsystemsAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikRoliProjektuEntity>> GetProjectRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikRoleSubsystemuEntity>> GetSubsystemRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikOrganizaceEntity>> GetOrganizationsAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, CiselnikOrganizacniCelekEntity>> GetOrgUnitsAsync(CancellationToken ct = default);
}
