using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Doplnění skutečnosti automatem při startu aplikace (db_upgrade_1_4_9, uživatel 2026-10-14).
/// Skript smaže historická data kroků 1/3/4/6/7/10 záznamů v automatickém režimu; automat je má
/// nahradit daty ze ServiceDesku. Harvest ale automat (SyncZaznamAsync) spouští jen při NOVÉM
/// vyjádření, takže kroky, ke kterým vyjádření už je navázané, by zůstaly prázdné.
///
/// Běží na pozadí, aby start aplikace nečekal na ServiceDesk. Vybere jen záznamy v automatickém
/// režimu, které mají prázdný krok 1/3/4/6/7/10 s aktivní navázanou vazbou na vyjádření — po
/// prvním běhu typicky nic. Selhání se zaloguje a start pokračuje (stejně jako
/// RichTextSearchTextBackfillHostedService); další start doplnění zopakuje.
/// </summary>
public sealed class HarmonogramAutomatDoplneniHostedService : BackgroundService
{
    private static readonly byte[] AutoKroky = [1, 3, 4, 6, 7, 10];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HarmonogramAutomatDoplneniHostedService> _logger;

    public HarmonogramAutomatDoplneniHostedService(
        IServiceScopeFactory scopeFactory, ILogger<HarmonogramAutomatDoplneniHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ať StartAsync hned vrátí a start aplikace nečeká na DB ani ServiceDesk.
        await Task.Yield();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            var sync = scope.ServiceProvider.GetRequiredService<IHarmonogramSkutecnostSyncService>();

            var pocet = await DoplnAsync(db, sync, stoppingToken);
            if (pocet > 0)
            {
                _logger.LogInformation("Automat doplnil skutečnost harmonogramu u {Pocet} záznamů.", pocet);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Doplnění skutečnosti harmonogramu automatem při startu selhalo; prázdné kroky se doplní " +
                "s příštím vyjádřením ze ServiceDesku nebo při příštím startu aplikace.");
        }
    }

    /// <returns>Počet záznamů, u kterých automat něco doplnil.</returns>
    public static async Task<int> DoplnAsync(
        PmTrackerDbContext db, IHarmonogramSkutecnostSyncService sync, CancellationToken ct)
    {
        // Záznam je v ručním režimu, když některý krok 1/3/4/6/7/10 je ruční (HarmonogramAutoFillRule).
        var rucniZaznamy = db.ZaznamHarmonogramKroky
            .Where(k => AutoKroky.Contains(k.Poradi) && k.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual)
            .Select(k => k.ZaznamId);

        var zaznamy = await db.VyjadreniVazby.AsNoTracking()
            .Where(v => v.Stav == (byte)VazbaStav.Active && AutoKroky.Contains(v.Poradi))
            .Where(v => !rucniZaznamy.Contains(v.ZaznamId))
            .Where(v => !db.ZaznamHarmonogramKroky.Any(k =>
                k.ZaznamId == v.ZaznamId && k.Poradi == v.Poradi && k.SkutecnostDatum != null))
            .Select(v => v.ZaznamId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(ct);

        var doplneno = 0;
        foreach (var zaznamId in zaznamy)
        {
            var vysledek = await sync.SyncZaznamAsync(zaznamId, ct);
            if (vysledek.KrokuAktualizovano > 0)
            {
                doplneno++;
            }
        }
        return doplneno;
    }
}
