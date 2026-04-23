using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services;

/// <summary>
/// Scoped fasáda nad sdíleným singleton <see cref="IMemoryCache"/>. Drží per-request
/// memo (pro <c>BeSameAs</c> identitu uvnitř requestu) nad shared cache s TTL
/// (cross-request persistence). Viz <see cref="ILookupTableCache"/> pro detaily.
/// </summary>
/// <remarks>
/// <para>
/// <b>TTL:</b> 5 minut. Ciselniky jsou stabilní (mění se pouze admin editem), stale
/// read v tomto okně je akceptovatelný. Produkční TTL lze ladit přes options v budoucnu.
/// </para>
/// <para>
/// <b>Klíče:</b> <c>"lookup:&lt;table&gt;"</c>. Všechny v jednom namespace, aby šly
/// hromadně invalidovat (admin UI pro editaci číselníku zavolá <c>_memory.Remove(key)</c>).
/// </para>
/// </remarks>
public sealed class LookupTableCache : ILookupTableCache
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly PmTrackerDbContext _db;
    private readonly IMemoryCache _memory;
    private readonly Dictionary<string, object> _requestMemo = new();

    public LookupTableCache(PmTrackerDbContext db, IMemoryCache memory)
    {
        _db = db;
        _memory = memory;
    }

    public Task<IReadOnlyDictionary<int, CiselnikKategoriiZaznamuEntity>> GetCategoriesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:categories", static (db, c)
            => db.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikStavuUkoluEntity>> GetTaskStatesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:task-states", static (db, c)
            => db.CiselnikStavuUkolu.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikTypuUkoluEntity>> GetTaskTypesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:task-types", static (db, c)
            => db.CiselnikTypuUkolu.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikStavuJednaniEntity>> GetMeetingStatesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:meeting-states", static (db, c)
            => db.CiselnikStavuJednani.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikTypuExternichOdkazuEntity>> GetExternalLinkTypesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:external-link-types", static (db, c)
            => db.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, VyzvaEntity>> GetVyzvyAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:vyzvy", static (db, c)
            => db.Vyzvy.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, SubsystemEntity>> GetSubsystemsAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:subsystems", static (db, c)
            => db.Subsystemy.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikRoliProjektuEntity>> GetProjectRolesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:project-roles", static (db, c)
            => db.CiselnikRoliProjektu.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikRoleSubsystemuEntity>> GetSubsystemRolesAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:subsystem-roles", static (db, c)
            => db.CiselnikRoliSubsystemu.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikOrganizaceEntity>> GetOrganizationsAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:organizations", static (db, c)
            => db.CiselnikOrganizace.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    public Task<IReadOnlyDictionary<int, CiselnikOrganizacniCelekEntity>> GetOrgUnitsAsync(CancellationToken ct = default)
        => GetOrLoadAsync("lookup:org-units", static (db, c)
            => db.CiselnikOrganizacniCelky.AsNoTracking().ToDictionaryAsync(x => x.Id, c), ct);

    private async Task<IReadOnlyDictionary<int, TValue>> GetOrLoadAsync<TValue>(
        string key,
        Func<PmTrackerDbContext, CancellationToken, Task<Dictionary<int, TValue>>> loader,
        CancellationToken ct)
        where TValue : class
    {
        // 1. Per-request memo: garantuje BeSameAs identitu pro více volání v rámci jednoho requestu
        //    a chrání před race-windowem, kdy by singleton cache expiroval mezi dvěma voláními
        //    stejné metody.
        if (_requestMemo.TryGetValue(key, out var memoized))
        {
            return (IReadOnlyDictionary<int, TValue>)memoized;
        }

        // 2. Sdílený IMemoryCache: perzistentní snapshot přes request boundaries s TTL.
        var snapshot = await _memory.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultTtl;
            var dict = await loader(_db, ct).ConfigureAwait(false);
            return (IReadOnlyDictionary<int, TValue>)dict;
        }).ConfigureAwait(false);

        _requestMemo[key] = snapshot!;
        return snapshot!;
    }
}
