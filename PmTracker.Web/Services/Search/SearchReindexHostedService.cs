using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Search;

public sealed class SearchReindexHostedService : BackgroundService
{
    private const int AuditBatchSize = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SearchOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SearchReindexHostedService> _logger;

    public SearchReindexHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SearchOptions> options,
        TimeProvider timeProvider,
        ILogger<SearchReindexHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        // Inbox #16 (2026-04-21): Startup bootstrap.
        // Starý stav: EnsureIndexAsync se volal JEN z IndexEntityAsync/FullReindexAsync.
        // Na nové DB (bez AuditLog entries pro existující data) se nikdy nezavolal → FTS index chyběl.
        // Fix: při startu zavolat EnsureIndexAsync + pokud je SearchIndex prázdný, udělat full reindex.
        await RunBootstrapAsync(stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.ReindexIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reindex cyklus selhal.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunBootstrapAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<ISearchClient>();
            var indexer = scope.ServiceProvider.GetRequiredService<ISearchIndexer>();

            // 1. EnsureIndex je idempotent — vytvoří tabulku, FTS katalog, FTS index (pokud chybí).
            await client.EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

            // 2. Ověř, zda je FTS index opravdu nakonfigurovaný. Pokud ne (typicky SQL account
            //    bez FTS permission), zaloguj hlasitě — admin musí zavolat DBA.
            var searchable = await client.IsSearchableAsync(cancellationToken).ConfigureAwait(false);
            if (!searchable)
            {
                _logger.LogError(
                    "FTS index na tabulce SearchIndex NENÍ nakonfigurovaný. " +
                    "Vyhledávání nebude fungovat, dokud DBA nespustí: " +
                    "CREATE FULLTEXT CATALOG SearchCatalog AS DEFAULT; " +
                    "CREATE FULLTEXT INDEX ON SearchIndex(Title, Body, Keywords) KEY INDEX <pk> ON SearchCatalog;");
                return;
            }

            // 3. Pokud je SearchIndex prázdný (new deploy / DB import bez auditu),
            //    spusť jednorázový full reindex.
            var docCount = await client.GetDocumentCountAsync(cancellationToken).ConfigureAwait(false);
            if (docCount == 0)
            {
                _logger.LogInformation("SearchIndex je prázdný — spouštím úvodní full reindex…");
                var indexed = await indexer.FullReindexAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Úvodní full reindex dokončen: {Count} dokumentů.", indexed);
            }
            else
            {
                _logger.LogInformation("SearchIndex připraven: {Count} dokumentů.", docCount);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Search bootstrap selhal. Aplikace pokračuje, ale vyhledávání může být omezené. " +
                "Super-admin může spustit reindex ručně z Nastavení.");
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var indexer = scope.ServiceProvider.GetRequiredService<ISearchIndexer>();

        var checkpoint = await db.SearchReindexCheckpoint.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken).ConfigureAwait(false);
        if (checkpoint is null)
        {
            checkpoint = new SearchReindexCheckpointEntity
            {
                Id = 1,
                LastProcessedAuditId = 0,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            db.SearchReindexCheckpoint.Add(checkpoint);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var lastId = checkpoint.LastProcessedAuditId;
        var entries = await db.AuthzAuditLog
            .AsNoTracking()
            .Where(e => e.Id > lastId)
            .OrderBy(e => e.Id)
            .Take(AuditBatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
        {
            return;
        }

        var dedup = new Dictionary<(string Type, string Id), AuthzAuditLogEntity>();
        foreach (var entry in entries)
        {
            var searchType = MapAuditTypeToSearchType(entry.EntityType);
            if (searchType is null)
            {
                continue;
            }
            dedup[(searchType, entry.EntityId)] = entry;
        }

        foreach (var kvp in dedup)
        {
            var (searchType, entityId) = kvp.Key;
            var entry = kvp.Value;
            try
            {
                if (IsDeleteAction(entry.Action))
                {
                    await indexer.DeleteEntityAsync(searchType, entityId, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await indexer.IndexEntityAsync(searchType, entityId, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reindex položky {Type}:{Id} selhal.", searchType, entityId);
            }
        }

        checkpoint.LastProcessedAuditId = entries[^1].Id;
        checkpoint.LastProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        checkpoint.UpdatedAt = checkpoint.LastProcessedAt.Value;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? MapAuditTypeToSearchType(string auditEntityType) => auditEntityType switch
    {
        "projekt" => EntityDocumentMapper.TypeProjekt,
        "zaznam" => EntityDocumentMapper.TypeZaznam,
        "jednani" => EntityDocumentMapper.TypeJednani,
        "vyjadreni" => EntityDocumentMapper.TypeVyjadreni,
        "osoba" => EntityDocumentMapper.TypeOsoba,
        "record_proposal" => EntityDocumentMapper.TypeZaznamNavrh,
        _ => null
    };

    private static bool IsDeleteAction(string action)
        => string.Equals(action, "delete", StringComparison.OrdinalIgnoreCase)
        || string.Equals(action, "soft_delete", StringComparison.OrdinalIgnoreCase);
}
