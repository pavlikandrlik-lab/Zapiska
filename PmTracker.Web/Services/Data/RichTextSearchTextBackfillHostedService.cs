using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

/// <summary>
/// Jednorázový dopočet čistého textu pro data uložená před db_upgrade_1_4_7 (uživatel
/// 2026-10-08). Běží při startu hned po kontrole schématu a doplní jen řádky, kde HTML je
/// a čistý text chybí (NULL). Po prvním běhu nemá co dělat. Nové a upravené texty plní
/// PmTrackerDbContext při uložení.
///
/// Review M3 (2026-10-08): dávka se stránkuje podle Id (ne OFFSET od začátku) — jinak dotaz
/// skenuje znovu od prvního řádku při každé dávce a práce roste s druhou mocninou počtu
/// řádků. Selhání (např. timeout DB při startu) se zaloguje a start aplikace pokračuje —
/// další start dopočet zopakuje. Dřív selhání shodilo celý hosting (500.30) za jednorázovou
/// dávkovou úlohu, ne chybějící předpoklad schématu.
/// </summary>
public sealed class RichTextSearchTextBackfillHostedService : IHostedService
{
    private const int Davka = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RichTextSearchTextBackfillHostedService> _logger;

    public RichTextSearchTextBackfillHostedService(
        IServiceScopeFactory scopeFactory, ILogger<RichTextSearchTextBackfillHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();

            await ZalogujPocetAsync("popis záznamů", DoplnPopisyAsync(db, ct));
            await ZalogujPocetAsync("text vyjádření", DoplnVyjadreniAsync(db, ct));
            await ZalogujPocetAsync("požadavek externích odkazů", DoplnPozadavkyAsync(db, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Dopočet čistého textu pro hledání selhal; hledání ve starších popisech a vyjádřeních " +
                "může být neúplné. Další start aplikace dopočet zopakuje.");
        }
    }

    private async Task ZalogujPocetAsync(string popisek, Task<int> dopln)
    {
        var pocet = await dopln;
        if (pocet > 0)
        {
            _logger.LogInformation("Čistý text pro hledání doplněn u {Pocet} řádků ({Popisek}).", pocet, popisek);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public static async Task<int> BackfillAsync(PmTrackerDbContext db, CancellationToken ct)
    {
        var celkem = 0;
        celkem += await DoplnPopisyAsync(db, ct);
        celkem += await DoplnVyjadreniAsync(db, ct);
        celkem += await DoplnPozadavkyAsync(db, ct);
        return celkem;
    }

    private static Task<int> DoplnPopisyAsync(PmTrackerDbContext db, CancellationToken ct) => DoplnAsync(
        lastId => db.ProjektoveZaznamy.Where(z => z.Id > lastId && z.Popis != null && z.PopisProstyText == null).OrderBy(z => z.Id),
        z => z.Id, z => z.PopisProstyText = RichTextSearchText.FromHtml(z.Popis), db, ct);

    private static Task<int> DoplnVyjadreniAsync(PmTrackerDbContext db, CancellationToken ct) => DoplnAsync(
        lastId => db.Vyjadreni.Where(v => v.Id > lastId && v.TextVyjadreni != null && v.TextVyjadreniProstyText == null).OrderBy(v => v.Id),
        v => v.Id, v => v.TextVyjadreniProstyText = RichTextSearchText.FromHtml(v.TextVyjadreni), db, ct);

    private static Task<int> DoplnPozadavkyAsync(PmTrackerDbContext db, CancellationToken ct) => DoplnAsync(
        lastId => db.ZaznamExterniOdkazy.Where(o => o.Id > lastId && o.Pozadavek != null && o.PozadavekProstyText == null).OrderBy(o => o.Id),
        o => o.Id, o => o.PozadavekProstyText = RichTextSearchText.FromHtml(o.Pozadavek), db, ct);

    // Stránkuje podle Id (Where Id > lastId), ne OFFSET od začátku — jinak dotaz skenuje
    // znovu od prvního řádku při každé dávce a práce roste s druhou mocninou počtu řádků
    // (review M3). dotaz dostane poslední Id dávky a vrátí frontu od něj dál; doplněné
    // řádky z dotazu vypadnou, protože FromHtml pro neprázdné HTML vrátí řetězec (i
    // prázdný), nikdy NULL.
    private static async Task<int> DoplnAsync<T>(
        Func<int, IOrderedQueryable<T>> dotaz, Func<T, int> id, Action<T> dopln, PmTrackerDbContext db, CancellationToken ct)
        where T : class
    {
        var pocet = 0;
        var posledniId = 0;
        while (true)
        {
            var davka = await dotaz(posledniId).Take(Davka).ToListAsync(ct);
            if (davka.Count == 0)
            {
                return pocet;
            }

            davka.ForEach(dopln);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            pocet += davka.Count;
            posledniId = davka.Max(id);
        }
    }
}
