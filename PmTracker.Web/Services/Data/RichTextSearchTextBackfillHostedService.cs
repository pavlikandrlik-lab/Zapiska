using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

/// <summary>
/// Jednorázový dopočet čistého textu pro data uložená před db_upgrade_1_4_7 (uživatel
/// 2026-10-08). Běží při startu hned po kontrole schématu a doplní jen řádky, kde HTML je
/// a čistý text chybí (NULL). Po prvním běhu nemá co dělat. Nové a upravené texty plní
/// PmTrackerDbContext při uložení.
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var doplneno = await BackfillAsync(db, ct);
        if (doplneno > 0)
        {
            _logger.LogInformation("Čistý text pro hledání doplněn u {Pocet} řádků.", doplneno);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public static async Task<int> BackfillAsync(PmTrackerDbContext db, CancellationToken ct)
    {
        var celkem = 0;

        celkem += await DoplnAsync(
            db.ProjektoveZaznamy.Where(z => z.Popis != null && z.PopisProstyText == null).OrderBy(z => z.Id),
            z => z.PopisProstyText = RichTextSearchText.FromHtml(z.Popis), db, ct);
        celkem += await DoplnAsync(
            db.Vyjadreni.Where(v => v.TextVyjadreni != null && v.TextVyjadreniProstyText == null).OrderBy(v => v.Id),
            v => v.TextVyjadreniProstyText = RichTextSearchText.FromHtml(v.TextVyjadreni), db, ct);
        celkem += await DoplnAsync(
            db.ZaznamExterniOdkazy.Where(o => o.Pozadavek != null && o.PozadavekProstyText == null).OrderBy(o => o.Id),
            o => o.PozadavekProstyText = RichTextSearchText.FromHtml(o.Pozadavek), db, ct);

        return celkem;
    }

    // Vybírá vždy znovu první dávku chybějících: doplněné řádky z dotazu vypadnou, protože
    // FromHtml pro neprázdné HTML vrátí řetězec (i prázdný), nikdy NULL. OrderBy(Id) na
    // volajícím místě je nutný kvůli deterministickému Take (jinak EF hlásí warning).
    private static async Task<int> DoplnAsync<T>(
        IOrderedQueryable<T> chybejici, Action<T> dopln, PmTrackerDbContext db, CancellationToken ct)
        where T : class
    {
        var pocet = 0;
        while (true)
        {
            var davka = await chybejici.Take(Davka).ToListAsync(ct);
            if (davka.Count == 0)
            {
                return pocet;
            }

            davka.ForEach(dopln);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            pocet += davka.Count;
        }
    }
}
