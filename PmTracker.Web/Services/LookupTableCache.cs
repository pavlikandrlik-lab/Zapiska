using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services;

/// <summary>
/// Scoped implementace <see cref="ILookupTableCache"/>. Při prvním volání stáhne celou
/// ciselnik tabulku do in-memory dictionary a drží ji po celou dobu requestu.
/// Následná volání vrátí stejnou instanci bez DB dotazu.
/// </summary>
/// <remarks>
/// Thread-safety: scoped lifetime + request-per-request zpracování v ASP.NET Core
/// garantuje single-threaded přístup. Žádné locky tedy nejsou potřeba.
/// </remarks>
public sealed class LookupTableCache : ILookupTableCache
{
    private readonly PmTrackerDbContext _db;

    private IReadOnlyDictionary<int, CiselnikKategoriiZaznamuEntity>? _categories;
    private IReadOnlyDictionary<int, CiselnikStavuUkoluEntity>? _taskStates;
    private IReadOnlyDictionary<int, CiselnikTypuUkoluEntity>? _taskTypes;
    private IReadOnlyDictionary<int, CiselnikStavuJednaniEntity>? _meetingStates;
    private IReadOnlyDictionary<int, CiselnikTypuExternichOdkazuEntity>? _externalLinkTypes;
    private IReadOnlyDictionary<int, VyzvaEntity>? _vyzvy;
    private IReadOnlyDictionary<int, SubsystemEntity>? _subsystems;
    private IReadOnlyDictionary<int, CiselnikRoliProjektuEntity>? _projectRoles;
    private IReadOnlyDictionary<int, CiselnikRoleSubsystemuEntity>? _subsystemRoles;
    private IReadOnlyDictionary<int, CiselnikOrganizaceEntity>? _organizations;
    private IReadOnlyDictionary<int, CiselnikOrganizacniCelekEntity>? _orgUnits;

    public LookupTableCache(PmTrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<int, CiselnikKategoriiZaznamuEntity>> GetCategoriesAsync(CancellationToken ct = default)
        => _categories ??= await _db.CiselnikKategoriiZaznamu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikStavuUkoluEntity>> GetTaskStatesAsync(CancellationToken ct = default)
        => _taskStates ??= await _db.CiselnikStavuUkolu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikTypuUkoluEntity>> GetTaskTypesAsync(CancellationToken ct = default)
        => _taskTypes ??= await _db.CiselnikTypuUkolu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikStavuJednaniEntity>> GetMeetingStatesAsync(CancellationToken ct = default)
        => _meetingStates ??= await _db.CiselnikStavuJednani.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikTypuExternichOdkazuEntity>> GetExternalLinkTypesAsync(CancellationToken ct = default)
        => _externalLinkTypes ??= await _db.CiselnikTypuExternichOdkazu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, VyzvaEntity>> GetVyzvyAsync(CancellationToken ct = default)
        => _vyzvy ??= await _db.Vyzvy.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, SubsystemEntity>> GetSubsystemsAsync(CancellationToken ct = default)
        => _subsystems ??= await _db.Subsystemy.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikRoliProjektuEntity>> GetProjectRolesAsync(CancellationToken ct = default)
        => _projectRoles ??= await _db.CiselnikRoliProjektu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikRoleSubsystemuEntity>> GetSubsystemRolesAsync(CancellationToken ct = default)
        => _subsystemRoles ??= await _db.CiselnikRoliSubsystemu.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikOrganizaceEntity>> GetOrganizationsAsync(CancellationToken ct = default)
        => _organizations ??= await _db.CiselnikOrganizace.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);

    public async Task<IReadOnlyDictionary<int, CiselnikOrganizacniCelekEntity>> GetOrgUnitsAsync(CancellationToken ct = default)
        => _orgUnits ??= await _db.CiselnikOrganizacniCelky.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);
}
