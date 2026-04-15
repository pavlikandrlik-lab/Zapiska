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
