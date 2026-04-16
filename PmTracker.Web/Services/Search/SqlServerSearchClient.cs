using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Search;

public sealed class SqlServerSearchClient : ISearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _connectionString;
    private readonly ILogger<SqlServerSearchClient> _logger;

    public SqlServerSearchClient(PmTrackerDbContext dbContext, ILogger<SqlServerSearchClient> logger)
    {
        _connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Nelze získat connection string z PmTrackerDbContext.");
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // EnsureIndexAsync
    // -------------------------------------------------------------------------

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            // 1. Create table if it doesn't exist
            await ExecuteNonQueryAsync(conn, """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.objects
                    WHERE object_id = OBJECT_ID(N'SearchIndex') AND type = 'U'
                )
                BEGIN
                    CREATE TABLE SearchIndex (
                        Id          INT IDENTITY(1,1) PRIMARY KEY,
                        DocumentId  NVARCHAR(200)  NOT NULL,
                        EntityType  NVARCHAR(50)   NOT NULL,
                        EntityId    NVARCHAR(50)   NOT NULL,
                        ProjektId   INT            NULL,
                        Title       NVARCHAR(500)  NOT NULL,
                        Body        NVARCHAR(MAX)  NOT NULL,
                        Keywords    NVARCHAR(1000) NOT NULL DEFAULT '',
                        MetaJson    NVARCHAR(MAX)  NOT NULL DEFAULT '{}',
                        UpdatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT UQ_SearchIndex_DocumentId UNIQUE (DocumentId)
                    );
                END
                """, cancellationToken).ConfigureAwait(false);

            // 2. Full-text catalog
            await ExecuteNonQueryAsync(conn, """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'SearchCatalog'
                )
                    CREATE FULLTEXT CATALOG SearchCatalog AS DEFAULT;
                """, cancellationToken).ConfigureAwait(false);

            // 3. Discover the actual PK index name (it may differ from the CONSTRAINT name)
            var pkIndexName = await ExecuteScalarAsync<string>(conn, """
                SELECT i.name
                FROM sys.indexes i
                JOIN sys.objects o ON o.object_id = i.object_id
                WHERE o.name = 'SearchIndex'
                  AND i.is_primary_key = 1
                """, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(pkIndexName))
            {
                _logger.LogWarning("Tabulka SearchIndex neobsahuje PK index — full-text index nebyl vytvořen.");
                return;
            }

            // 4. Full-text index (idempotent guard via sys.fulltext_indexes)
            await ExecuteNonQueryAsync(conn, $"""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.fulltext_indexes
                    WHERE object_id = OBJECT_ID('SearchIndex')
                )
                    CREATE FULLTEXT INDEX ON SearchIndex(Title, Body, Keywords)
                        KEY INDEX [{pkIndexName}]
                        ON SearchCatalog;
                """, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("SearchIndex a full-text index jsou připraveny.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při inicializaci SearchIndex.");
        }
    }

    // -------------------------------------------------------------------------
    // BulkIndexAsync
    // -------------------------------------------------------------------------

    public async Task BulkIndexAsync(
        IReadOnlyCollection<SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return;
        }

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in documents)
            {
                var keywords = string.Join(' ', doc.Keywords);
                var metaJson = JsonSerializer.Serialize(doc.Meta, JsonOptions);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    MERGE SearchIndex WITH (HOLDLOCK) AS target
                    USING (SELECT @DocumentId AS DocumentId) AS source
                    ON target.DocumentId = source.DocumentId
                    WHEN MATCHED THEN
                        UPDATE SET
                            EntityType = @EntityType,
                            EntityId   = @EntityId,
                            ProjektId  = @ProjektId,
                            Title      = @Title,
                            Body       = @Body,
                            Keywords   = @Keywords,
                            MetaJson   = @MetaJson,
                            UpdatedAt  = @UpdatedAt
                    WHEN NOT MATCHED THEN
                        INSERT (DocumentId, EntityType, EntityId, ProjektId, Title, Body, Keywords, MetaJson, UpdatedAt)
                        VALUES (@DocumentId, @EntityType, @EntityId, @ProjektId, @Title, @Body, @Keywords, @MetaJson, @UpdatedAt);
                    """;

                cmd.Parameters.AddWithValue("@DocumentId", doc.DocumentId);
                cmd.Parameters.AddWithValue("@EntityType", doc.EntityType);
                cmd.Parameters.AddWithValue("@EntityId", doc.EntityId);
                cmd.Parameters.AddWithValue("@ProjektId", (object?)doc.ProjektId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Title", doc.Title);
                cmd.Parameters.AddWithValue("@Body", doc.Body);
                cmd.Parameters.AddWithValue("@Keywords", keywords);
                cmd.Parameters.AddWithValue("@MetaJson", metaJson);
                cmd.Parameters.AddWithValue("@UpdatedAt", doc.UpdatedAt);

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("BulkIndex: zpracováno {Count} dokumentů.", documents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při bulk indexování do SearchIndex.");
        }
    }

    // -------------------------------------------------------------------------
    // DeleteDocumentAsync
    // -------------------------------------------------------------------------

    public async Task DeleteDocumentAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM SearchIndex
                WHERE EntityType = @EntityType AND EntityId = @EntityId;
                """;
            cmd.Parameters.AddWithValue("@EntityType", entityType);
            cmd.Parameters.AddWithValue("@EntityId", entityId);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("DeleteDocument: odstraněno {Rows} řádků pro {EntityType}:{EntityId}.",
                affected, entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při mazání dokumentu {EntityType}:{EntityId} z SearchIndex.",
                entityType, entityId);
        }
    }

    // -------------------------------------------------------------------------
    // SearchAsync
    // -------------------------------------------------------------------------

    public async Task<SearchQueryResponse> SearchAsync(
        SearchQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new SearchQueryResponse { Hits = Array.Empty<SearchHit>(), TotalCandidates = 0 };
        }

        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();

            var hasProjectFilter = request.PreFilterProjectIds is { Count: > 0 };

            if (hasProjectFilter)
            {
                // Inline the project IDs as integer literals — safe because they are typed as int.
                var ids = string.Join(',', request.PreFilterProjectIds!.Select(id => id.ToString()));
                cmd.CommandText = $"""
                    SELECT TOP (@Size)
                        si.EntityType,
                        si.EntityId,
                        si.ProjektId,
                        si.Title,
                        LEFT(si.Body, 200)  AS Snippet,
                        ft.[RANK]           AS Score,
                        si.MetaJson
                    FROM FREETEXTTABLE(SearchIndex, (Title, Body, Keywords), @Query) AS ft
                    JOIN SearchIndex AS si ON si.Id = ft.[KEY]
                    WHERE si.ProjektId IS NULL OR si.ProjektId IN ({ids})
                    ORDER BY ft.[RANK] DESC;
                    """;
            }
            else
            {
                cmd.CommandText = """
                    SELECT TOP (@Size)
                        si.EntityType,
                        si.EntityId,
                        si.ProjektId,
                        si.Title,
                        LEFT(si.Body, 200)  AS Snippet,
                        ft.[RANK]           AS Score,
                        si.MetaJson
                    FROM FREETEXTTABLE(SearchIndex, (Title, Body, Keywords), @Query) AS ft
                    JOIN SearchIndex AS si ON si.Id = ft.[KEY]
                    ORDER BY ft.[RANK] DESC;
                    """;
            }

            cmd.Parameters.AddWithValue("@Query", request.Query);
            cmd.Parameters.AddWithValue("@Size", request.Size);

            var hits = new List<SearchHit>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var metaJson = reader.IsDBNull(6) ? "{}" : reader.GetString(6);
                var meta = DeserializeMeta(metaJson);

                hits.Add(new SearchHit
                {
                    EntityType = reader.GetString(0),
                    EntityId   = reader.GetString(1),
                    ProjektId  = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Title      = reader.GetString(3),
                    Snippet    = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Score      = reader.GetInt32(5),   // FREETEXTTABLE RANK is INT
                    Meta       = meta
                });
            }

            return new SearchQueryResponse
            {
                Hits             = hits,
                TotalCandidates  = hits.Count
            };
        }
        catch (SqlException ex) when (ex.Message.Contains("FREETEXTTABLE", StringComparison.OrdinalIgnoreCase)
                                       || ex.Message.Contains("full-text", StringComparison.OrdinalIgnoreCase)
                                       || ex.Message.Contains("fulltext", StringComparison.OrdinalIgnoreCase))
        {
            // Full-text index not yet populated or unavailable — return empty gracefully
            _logger.LogWarning(ex, "Full-text index není dosud dostupný, vracím prázdný výsledek.");
            return new SearchQueryResponse { Hits = Array.Empty<SearchHit>(), TotalCandidates = 0 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chyba při fulltextovém vyhledávání pro dotaz '{Query}'.", request.Query);
            return new SearchQueryResponse { Hits = Array.Empty<SearchHit>(), TotalCandidates = 0 };
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task ExecuteNonQueryAsync(
        SqlConnection conn,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> ExecuteScalarAsync<T>(
        SqlConnection conn,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (result is null || result == DBNull.Value)
        {
            return default;
        }
        return (T)result;
    }

    private static IReadOnlyDictionary<string, string?> DeserializeMeta(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)
                   ?? new Dictionary<string, string?>();
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }
}
