using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Search;

public sealed class SearchIndexer : ISearchIndexer
{
    private const int BulkBatchSize = 500;

    private readonly PmTrackerDbContext _db;
    private readonly ISearchClient _client;
    private readonly TimeProvider _timeProvider;

    public SearchIndexer(PmTrackerDbContext db, ISearchClient client, TimeProvider timeProvider)
    {
        _db = db;
        _client = client;
        _timeProvider = timeProvider;
    }

    public async Task IndexEntityAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(entityId, out var id))
        {
            return;
        }

        var doc = await BuildDocumentAsync(entityType, id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            await _client.DeleteDocumentAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _client.EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        await _client.BulkIndexAsync(new[] { doc }, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteEntityAsync(string entityType, string entityId, CancellationToken cancellationToken)
        => _client.DeleteDocumentAsync(entityType, entityId, cancellationToken);

    public async Task<int> FullReindexAsync(CancellationToken cancellationToken)
    {
        await _client.EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var total = 0;

        total += await BulkAsync(
            (await _db.Projekty.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(p => EntityDocumentMapper.MapProjekt(p, now)),
            cancellationToken).ConfigureAwait(false);

        total += await BulkAsync(
            (await _db.ProjektoveZaznamy.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(z => EntityDocumentMapper.MapZaznam(z, now)),
            cancellationToken).ConfigureAwait(false);

        total += await BulkAsync(
            (await _db.Jednani.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(j => EntityDocumentMapper.MapJednani(j, now)),
            cancellationToken).ConfigureAwait(false);

        total += await BulkAsync(
            (await _db.Osoby.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(o => EntityDocumentMapper.MapOsoba(o, now)),
            cancellationToken).ConfigureAwait(false);

        total += await BulkAsync(
            (await _db.Subsystemy.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(s => EntityDocumentMapper.MapSubsystem(s, now)),
            cancellationToken).ConfigureAwait(false);

        var vyjadreniData = await (
            from v in _db.Vyjadreni.AsNoTracking()
            join j in _db.Jednani.AsNoTracking() on v.JednaniId equals j.Id
            select new { Vyjadreni = v, ProjektId = j.ProjektId }
        ).ToListAsync(cancellationToken).ConfigureAwait(false);

        total += await BulkAsync(
            vyjadreniData.Select(x => EntityDocumentMapper.MapVyjadreni(x.Vyjadreni, x.ProjektId, now)),
            cancellationToken).ConfigureAwait(false);

        total += await BulkAsync(
            (await _db.ZaznamNavrhy.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(n => EntityDocumentMapper.MapZaznamNavrh(n, now)),
            cancellationToken).ConfigureAwait(false);

        return total;
    }

    private async Task<SearchDocument?> BuildDocumentAsync(string entityType, int id, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        switch (entityType)
        {
            case EntityDocumentMapper.TypeProjekt:
            {
                var p = await _db.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                return p is null ? null : EntityDocumentMapper.MapProjekt(p, now);
            }
            case EntityDocumentMapper.TypeZaznam:
            {
                var z = await _db.ProjektoveZaznamy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                return z is null ? null : EntityDocumentMapper.MapZaznam(z, now);
            }
            case EntityDocumentMapper.TypeJednani:
            {
                var j = await _db.Jednani.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                return j is null ? null : EntityDocumentMapper.MapJednani(j, now);
            }
            case EntityDocumentMapper.TypeOsoba:
            {
                var o = await _db.Osoby.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                return o is null ? null : EntityDocumentMapper.MapOsoba(o, now);
            }
            case EntityDocumentMapper.TypeSubsystem:
            {
                var s = await _db.Subsystemy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                return s is null ? null : EntityDocumentMapper.MapSubsystem(s, now);
            }
            case EntityDocumentMapper.TypeVyjadreni:
            {
                var data = await (
                    from v in _db.Vyjadreni.AsNoTracking()
                    join j in _db.Jednani.AsNoTracking() on v.JednaniId equals j.Id
                    where v.Id == id
                    select new { Vyjadreni = v, ProjektId = j.ProjektId }
                ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                return data is null ? null : EntityDocumentMapper.MapVyjadreni(data.Vyjadreni, data.ProjektId, now);
            }
            case EntityDocumentMapper.TypeZaznamNavrh:
            {
                var n = await _db.ZaznamNavrhy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
                return n is null ? null : EntityDocumentMapper.MapZaznamNavrh(n, now);
            }
            default:
                return null;
        }
    }

    private async Task<int> BulkAsync(IEnumerable<SearchDocument> docs, CancellationToken cancellationToken)
    {
        var batch = new List<SearchDocument>(BulkBatchSize);
        var total = 0;
        foreach (var d in docs)
        {
            batch.Add(d);
            if (batch.Count >= BulkBatchSize)
            {
                await _client.BulkIndexAsync(batch, cancellationToken).ConfigureAwait(false);
                total += batch.Count;
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            await _client.BulkIndexAsync(batch, cancellationToken).ConfigureAwait(false);
            total += batch.Count;
        }
        return total;
    }
}
