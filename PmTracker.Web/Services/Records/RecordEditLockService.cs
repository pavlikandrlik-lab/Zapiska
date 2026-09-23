using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Výsledek pokusu o získání zámku. Při neúspěchu nese držitele, aby ho šlo pojmenovat.
/// </summary>
public sealed record RecordEditLockResult(bool Acquired, int? HolderOsobaId, DateTime? HolderSinceUtc);

public interface IRecordEditLockService
{
    Task<RecordEditLockResult> TryAcquireAsync(int zaznamId, int osobaId, CancellationToken ct = default);
    Task HeartbeatAsync(int zaznamId, int osobaId, CancellationToken ct = default);
    Task ReleaseAsync(int zaznamId, int osobaId, CancellationToken ct = default);
}

/// <summary>
/// Spec 2026-09-17 §4 — advisory zámek karty záznamu.
///
/// Získání je jediný atomický MERGE, aby dva souběžné požadavky nemohly zámek dostat
/// oba. Zámek dostane ten, kdo ho už drží (re-entrance pro druhý tab téhož uživatele),
/// nebo ten, čí předchůdce se déle než <see cref="Ttl"/> neozval.
///
/// Zámek je pojistka proti souběhu, ne jediná obrana: i když vyprší nebo ho někdo obejde,
/// cizí zápis zachytí record guard (§5) a uložení odmítne se jménem autora.
/// </summary>
public sealed class RecordEditLockService(PmTrackerDbContext dbContext, TimeProvider timeProvider)
    : IRecordEditLockService
{
    /// <summary>15 minut bez heartbeatu = 3 zmeškané keep-alive cykly.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    public async Task<RecordEditLockResult> TryAcquireAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiryUtc = nowUtc - Ttl;

        // HOLDLOCK drží rozsah po dobu MERGE — bez něj by dva souběžné INSERTy skončily
        // buď duplicitou, nebo porušením PK. Čas získání se při re-entrance nepřepisuje,
        // aby hláška druhému uživateli ukazovala, odkdy se záznam opravdu edituje.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
MERGE dbo.zaznam_edit_zamek WITH (HOLDLOCK) AS target
USING (SELECT {zaznamId} AS zaznam_id) AS source
    ON target.zaznam_id = source.zaznam_id
WHEN MATCHED AND (target.osoba_id = {osobaId} OR target.heartbeat_at < {expiryUtc})
    THEN UPDATE SET osoba_id = {osobaId},
                    ziskano_at = CASE WHEN target.osoba_id = {osobaId} THEN target.ziskano_at ELSE {nowUtc} END,
                    heartbeat_at = {nowUtc}
WHEN NOT MATCHED
    THEN INSERT (zaznam_id, osoba_id, ziskano_at, heartbeat_at)
         VALUES ({zaznamId}, {osobaId}, {nowUtc}, {nowUtc});", ct);

        var zamek = await dbContext.ZaznamEditZamky.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ZaznamId == zaznamId, ct);

        // zamek is null znamená, že řádek mezi MERGE a čtením někdo smazal (uvolnil zámek).
        // Fail-open je tu správně: blokovat uživatele kvůli závodu, po kterém je záznam volný,
        // by bylo horší než ho pustit dál — cizí zápis stejně zachytí record guard (§5).
        if (zamek is null || zamek.OsobaId == osobaId)
        {
            return new RecordEditLockResult(true, osobaId, zamek?.ZiskanoAt);
        }

        return new RecordEditLockResult(false, zamek.OsobaId, zamek.ZiskanoAt);
    }

    public async Task HeartbeatAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.ZaznamEditZamky
            .Where(x => x.ZaznamId == zaznamId && x.OsobaId == osobaId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeartbeatAt, nowUtc), ct);
    }

    public async Task ReleaseAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        // Maže výhradně VLASTNÍ zámek — cizí uvolnit nelze ani omylem, ani schválně.
        await dbContext.ZaznamEditZamky
            .Where(x => x.ZaznamId == zaznamId && x.OsobaId == osobaId)
            .ExecuteDeleteAsync(ct);
    }
}
