using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services.Data;

public sealed record HarmonogramTypPar(
    int KrokIndex,
    string Kod,
    string Nazev,
    string BarvaHex,
    int TrvaniTypId,
    int ZpozdeniTypId);

public sealed record HarmonogramSchemaDefinition(
    int Verze,
    string DelayBarvaHex,
    IReadOnlyList<HarmonogramTypPar> Kroky);

public sealed record HarmonogramSchemaCloneResult(
    HarmonogramSablonaEntity SourceSchema,
    HarmonogramSablonaEntity NewSchema,
    IReadOnlyDictionary<int, HarmonogramTypEntity> ClonedBySourceId,
    IReadOnlyList<HarmonogramTypEntity> ClonedRows);

public sealed record HarmonogramVypocetKroku(
    int KrokIndex,
    string Kod,
    string Nazev,
    string BarvaHex,
    int TrvaniTypId,
    int ZpozdeniTypId,
    int TrvaniDni,
    int ZpozdeniDni,
    DateTime PlanStartDatum,
    DateTime BaselineDatum,
    DateTime RealStartDatum,
    DateTime PosunuteDatum);

internal sealed class HarmonogramService(
    PmTrackerDbContext dbContext,
    IAuditWriteService auditWriteService,
    TimeProvider timeProvider,
    ILogger<HarmonogramService> logger) : IHarmonogramService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const string DefaultDelayBarvaHex = "#DC2626";
    private static readonly (int Poradi, string Kod, string Nazev, string BarvaHex)[] DefaultHarmonogramKroky =
    [
        (1, "HS01_DURATION", "1. priprava zadani dodavateli", "#EF4444"),
        (2, "HS02_DURATION", "2. konzultace terminu s dodavatelem pred vytvorenim zadani", "#F97316"),
        (3, "HS03_DURATION", "3. odeslani zadani dodavateli", "#F59E0B"),
        (4, "HS04_DURATION", "4. dodani navrhu reseni", "#84CC16"),
        (5, "HS05_DURATION", "5. vyporadani pripominek", "#22C55E"),
        (6, "HS06_DURATION", "6. odeslani pozadavku na vyrobu", "#14B8A6"),
        (7, "HS07_DURATION", "7. dodani funkcionality dodavatelem", "#06B6D4"),
        (8, "HS08_DURATION", "8. pripominkovani", "#3B82F6"),
        (9, "HS09_DURATION", "9. testovani", "#6366F1"),
        (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6")
    ];

    public Task<HarmonogramSchemaDefinition> GetActiveHarmonogramSchemaAsync(CancellationToken ct = default)
        => LoadActiveHarmonogramSchemaAsync(ct);

    public Task<HarmonogramSchemaDefinition> GetSchemaForRecordAsync(ProjektovyZaznamEntity record, CancellationToken ct = default)
        => GetSchemaForRecordAsync(record.HarmonogramSablonaVerze, ct);

    public Task<HarmonogramSchemaDefinition> GetSchemaForRecordAsync(int schemaVersion, CancellationToken ct = default)
        => LoadHarmonogramSchemaAsync(schemaVersion, ct);

    public IReadOnlyList<RecordScheduleTypeDefinition> BuildRecordScheduleTypeDefinitions(HarmonogramSchemaDefinition schema)
        => BuildRecordScheduleTypeDefinitionsCore(schema);

    public IReadOnlyList<HarmonogramVypocetKroku> BuildHarmonogramVypocetPublic(
        DateTime datumZalozeni,
        IReadOnlyList<HarmonogramTypPar> typy,
        IReadOnlyDictionary<int, int>? hodnoty)
        => BuildHarmonogramVypocetCore(datumZalozeni, typy, hodnoty);

    public HarmonogramSouhrnViewModel BuildHarmonogramSouhrn(IReadOnlyList<HarmonogramVypocetKroku> kroky, DateTime terminUkolu)
        => BuildHarmonogramSouhrnCore(kroky, terminUkolu);

    public async Task<int> EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct = default)
    {
        var activeSchema = await dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .FirstOrDefaultAsync(x => x.IsAktivni, ct);

        if (activeSchema is null)
        {
            activeSchema = new HarmonogramSablonaEntity
            {
                DelayBarvaHex = DefaultDelayBarvaHex,
                IsAktivni = true,
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
                CreatedBy = null
            };
            dbContext.HarmonogramSablony.Add(activeSchema);
            await dbContext.SaveChangesAsync(ct);
            auditWriteService.Add(null, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.Dictionary,
                $"harmonogram-schema:{activeSchema.Verze}",
                null,
                new DictionaryAuditSnapshot(
                    "harmonogram-schema",
                    activeSchema.Verze,
                    $"SCHEMA_V{activeSchema.Verze}",
                    "Aktivni harmonogramova sablona",
                    true,
                    true,
                    NormalizeHexColor(activeSchema.DelayBarvaHex, DefaultDelayBarvaHex))));
            await dbContext.SaveChangesAsync(ct);
        }

        var hasRows = await dbContext.CiselnikHarmonogramTypu
            .AnyAsync(x => x.SablonaVerze == activeSchema.Verze, ct);

        if (!hasRows)
        {
            var defaultRows = BuildDefaultHarmonogramTypeRows(activeSchema.Verze);
            dbContext.CiselnikHarmonogramTypu.AddRange(defaultRows);
            await dbContext.SaveChangesAsync(ct);
            foreach (var row in defaultRows)
            {
                auditWriteService.Add(null, new AuditWriteEntry(
                    AuditActionType.Create,
                    AuditEntityType.Dictionary,
                    row.Id.ToString(),
                    null,
                    new DictionaryAuditSnapshot(
                        "harmonogram-kroky",
                        row.Id,
                        row.Kod,
                        row.Nazev,
                        row.IsLocked,
                        true,
                        row.BarvaHex)));
            }

            await NormalizeHarmonogramSchemaRowsAsync(activeSchema.Verze, ct);
            await dbContext.SaveChangesAsync(ct);
        }

        return activeSchema.Verze;
    }

    private async Task<HarmonogramSchemaDefinition> LoadActiveHarmonogramSchemaAsync(CancellationToken ct)
    {
        try
        {
            var schema = await dbContext.HarmonogramSablony
                .AsNoTracking()
                .OrderByDescending(x => x.IsAktivni)
                .ThenByDescending(x => x.Verze)
                .FirstOrDefaultAsync(ct);
            if (schema is null)
            {
                return BuildFallbackSchemaDefinition();
            }

            var steps = await LoadHarmonogramTypyAsync(schema.Verze, ct);
            if (steps.Count == 0)
            {
                return BuildFallbackSchemaDefinition(schema.Verze, schema.DelayBarvaHex);
            }

            return new HarmonogramSchemaDefinition(
                schema.Verze,
                NormalizeHexColor(schema.DelayBarvaHex, DefaultDelayBarvaHex),
                steps);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return BuildFallbackSchemaDefinition();
        }
    }

    private async Task<HarmonogramSchemaDefinition> LoadHarmonogramSchemaAsync(int schemaVersion, CancellationToken ct)
    {
        if (schemaVersion <= 0)
        {
            return await LoadActiveHarmonogramSchemaAsync(ct);
        }

        try
        {
            var schema = await dbContext.HarmonogramSablony
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Verze == schemaVersion, ct);
            if (schema is null)
            {
                // F-06: Fallback to active schema when requested version not found
                logger.LogWarning("Harmonogram fallback: schema version {SchemaVersion} not found in database, falling back to active schema.", schemaVersion);
                return await LoadActiveHarmonogramSchemaAsync(ct);
            }

            var steps = await LoadHarmonogramTypyAsync(schemaVersion, ct);
            if (steps.Count == 0)
            {
                return BuildFallbackSchemaDefinition(schema.Verze, schema.DelayBarvaHex);
            }

            return new HarmonogramSchemaDefinition(
                schema.Verze,
                NormalizeHexColor(schema.DelayBarvaHex, DefaultDelayBarvaHex),
                steps);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            // F-06: Fallback due to missing schema
            logger.LogWarning(ex, "Harmonogram fallback: catalog schema tables missing for version {SchemaVersion}, using built-in defaults.", schemaVersion);
            return BuildFallbackSchemaDefinition(schemaVersion);
        }
    }

    private async Task<List<HarmonogramTypPar>> LoadHarmonogramTypyAsync(int schemaVersion, CancellationToken ct)
    {
        List<HarmonogramTypEntity> typRows;
        try
        {
            typRows = await dbContext.CiselnikHarmonogramTypu
                .AsNoTracking()
                .Where(x => x.SablonaVerze == schemaVersion)
                .OrderBy(x => x.KrokPoradi)
                .ThenBy(x => x.JeZpozdeni)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return [];
        }

        if (typRows.Count == 0)
        {
            return [];
        }

        var durationRows = typRows
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();
        var delayRows = typRows
            .Where(x => x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();

        var result = new List<HarmonogramTypPar>(durationRows.Count);
        var krokIndex = 1;
        foreach (var duration in durationRows)
        {
            // F-19: KrokKey párování - fallback na KrokPoradi pokud KrokKey selže
            var delayByKey = delayRows.FirstOrDefault(x => x.KrokKey == duration.KrokKey);
            if (delayByKey is null)
            {
                logger.LogWarning(
                    "Harmonogram fallback F-19: KrokKey match failed for duration row Id={DurationId} KrokKey={KrokKey} in schema version {SchemaVersion}, falling back to KrokPoradi={KrokPoradi} match.",
                    duration.Id, duration.KrokKey, schemaVersion, duration.KrokPoradi);
            }
            var delayType = delayByKey ?? delayRows.FirstOrDefault(x => x.KrokPoradi == duration.KrokPoradi);

            result.Add(new HarmonogramTypPar(
                krokIndex,
                string.IsNullOrWhiteSpace(duration.Kod) ? $"STEP_{krokIndex:00}" : duration.Kod.Trim(),
                string.IsNullOrWhiteSpace(duration.Nazev) ? $"Krok {krokIndex}" : duration.Nazev.Trim(),
                NormalizeHexColor(duration.BarvaHex, ResolveDefaultStepColor(krokIndex)),
                duration.Id,
                delayType?.Id ?? 0));
            krokIndex += 1;
        }

        return result;
    }

    private static IReadOnlyList<RecordScheduleTypeDefinition> BuildRecordScheduleTypeDefinitionsCore(HarmonogramSchemaDefinition schema)
    {
        return schema.Kroky
            .Select(step => new RecordScheduleTypeDefinition(step.TrvaniTypId, step.ZpozdeniTypId))
            .ToList();
    }

    private static IReadOnlyList<HarmonogramVypocetKroku> BuildHarmonogramVypocetCore(
        DateTime datumZalozeni,
        IReadOnlyList<HarmonogramTypPar> harmonogramTypy,
        IReadOnlyDictionary<int, int>? harmonogramHodnoty)
    {
        var definitions = (harmonogramTypy.Count == 0
                ? DefaultHarmonogramKroky
                    .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                    .ToList()
                : harmonogramTypy.OrderBy(x => x.KrokIndex).ToList())
            .Select(type => new ScheduleTimelineStepDefinition
            {
                StepIndex = type.KrokIndex,
                Code = type.Kod,
                Name = type.Nazev,
                ColorHex = NormalizeHexColor(type.BarvaHex, ResolveDefaultStepColor(type.KrokIndex)),
                DurationTypeId = type.TrvaniTypId,
                OffsetTypeId = type.ZpozdeniTypId
            })
            .ToList();

        var computation = ScheduleTimelineCalculator.Compute(datumZalozeni, definitions, harmonogramHodnoty);

        return computation.Steps
            .Select(step => new HarmonogramVypocetKroku(
                step.StepIndex,
                step.Code,
                step.Name,
                step.ColorHex,
                step.DurationTypeId,
                step.OffsetTypeId,
                step.DurationDays,
                step.OffsetDays,
                step.PlanStartDate,
                step.PlanEndDate,
                step.ActualStartDate,
                step.ActualEndDate))
            .ToList();
    }

    private static HarmonogramSouhrnViewModel BuildHarmonogramSouhrnCore(
        IReadOnlyList<HarmonogramVypocetKroku> kroky,
        DateTime terminUkolu)
    {
        var summary = ScheduleTimelineCalculator.Summarize(
            new ScheduleTimelineComputation
            {
                Steps = kroky
                    .Select(step => new ScheduleTimelineStepResult
                    {
                        StepIndex = step.KrokIndex,
                        Code = step.Kod,
                        Name = step.Nazev,
                        ColorHex = step.BarvaHex,
                        DurationTypeId = step.TrvaniTypId,
                        OffsetTypeId = step.ZpozdeniTypId,
                        DurationDays = step.TrvaniDni,
                        OffsetDays = step.ZpozdeniDni,
                        PlanStartDate = step.PlanStartDatum,
                        PlanEndDate = step.BaselineDatum,
                        ActualStartDate = step.RealStartDatum,
                        ActualEndDate = step.PosunuteDatum
                    })
                    .ToList(),
                TotalDurationDays = kroky.Sum(x => x.TrvaniDni),
                TotalOffsetDays = kroky.Sum(x => x.ZpozdeniDni)
            },
            terminUkolu);

        return new HarmonogramSouhrnViewModel
        {
            BaselineDokonceni = summary.BaselineCompletion,
            SkutecneDokonceni = summary.ActualCompletion,
            TerminUkolu = summary.Deadline,
            CelkoveTrvaniDni = summary.TotalDurationDays,
            CelkovaOdchylkaDni = summary.TotalOffsetDays,
            Stihame = summary.IsOnTrack,
            PrekroceniDni = Math.Max(0, summary.OverrunDays)
        };
    }

    private static HarmonogramSchemaDefinition BuildFallbackSchemaDefinition(int version = 0, string? delayBarvaHex = null)
    {
        return new HarmonogramSchemaDefinition(
            version,
            NormalizeHexColor(delayBarvaHex, DefaultDelayBarvaHex),
            DefaultHarmonogramKroky
                .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                .ToList());
    }

    private static List<HarmonogramTypEntity> BuildDefaultHarmonogramTypeRows(int schemaVersion)
    {
        var rows = new List<HarmonogramTypEntity>(DefaultHarmonogramKroky.Length * 2);

        foreach (var step in DefaultHarmonogramKroky)
        {
            var stepKey = Guid.NewGuid();
            rows.Add(new HarmonogramTypEntity
            {
                Kod = step.Kod,
                Nazev = step.Nazev,
                Hodnota = step.Poradi,
                IsLocked = true,
                SablonaVerze = schemaVersion,
                KrokKey = stepKey,
                KrokPoradi = step.Poradi,
                JeZpozdeni = false,
                BarvaHex = step.BarvaHex
            });

            rows.Add(new HarmonogramTypEntity
            {
                Kod = step.Kod.Replace("_DURATION", "_DELAY", StringComparison.OrdinalIgnoreCase),
                Nazev = $"{step.Nazev} - zpozdeni",
                Hodnota = 100 + step.Poradi,
                IsLocked = true,
                SablonaVerze = schemaVersion,
                KrokKey = stepKey,
                KrokPoradi = step.Poradi,
                JeZpozdeni = true,
                BarvaHex = null
            });
        }

        return rows;
    }

    private static string ResolveDefaultStepColor(int stepIndex)
    {
        var matched = DefaultHarmonogramKroky.FirstOrDefault(step => step.Poradi == stepIndex);
        if (matched.Poradi > 0 && !string.IsNullOrWhiteSpace(matched.BarvaHex))
        {
            return matched.BarvaHex;
        }

        return DefaultHarmonogramKroky.Length > 0 ? DefaultHarmonogramKroky[0].BarvaHex : "#94A3B8";
    }

    private static bool IsValidHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length != 7 || normalized[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < normalized.Length; i += 1)
        {
            var c = normalized[i];
            var isDigit = c >= '0' && c <= '9';
            var isLowerHex = c >= 'a' && c <= 'f';
            var isUpperHex = c >= 'A' && c <= 'F';
            if (!isDigit && !isLowerHex && !isUpperHex)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeHexColor(string? value, string fallback)

    {
        if (!IsValidHexColor(value))
        {
            return fallback.ToUpperInvariant();
        }

        return value!.Trim().ToUpperInvariant();
    }

    private static bool IsMissingHarmonogramCatalogSchema(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && (sqlException.Number == 208 || sqlException.Number == 207))
            {
                return true;
            }
        }

        return false;
    }

    private async Task NormalizeHarmonogramSchemaRowsAsync(int schemaVersion, CancellationToken ct)
    {
        var rows = await dbContext.CiselnikHarmonogramTypu
            .Where(x => x.SablonaVerze == schemaVersion)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.JeZpozdeni)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var durationRows = rows
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();

        var durationKeySet = durationRows.Select(x => x.KrokKey).ToHashSet();
        var orphanDelayRows = rows.Where(x => x.JeZpozdeni && !durationKeySet.Contains(x.KrokKey)).ToList();
        if (orphanDelayRows.Count > 0)
        {
            dbContext.CiselnikHarmonogramTypu.RemoveRange(orphanDelayRows);
        }

        // F-18: Optimization - precompute delay rows by KrokKey to avoid O(n²) complexity
        var delayRowsByKrokKey = rows
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).ToList());

        for (var index = 0; index < durationRows.Count; index += 1)
        {
            var order = index + 1;
            var duration = durationRows[index];
            duration.KrokPoradi = order;
            duration.Hodnota = order;
            duration.BarvaHex = NormalizeHexColor(duration.BarvaHex, ResolveDefaultStepColor(order));

            var delayRows = delayRowsByKrokKey.TryGetValue(duration.KrokKey, out var dr) ? dr : new List<HarmonogramTypEntity>();
            foreach (var delay in delayRows)
            {
                delay.KrokPoradi = order;
                delay.Hodnota = 100 + order;
                delay.BarvaHex = null;
            }

            if (delayRows.Count == 0)
            {
                var fallbackDelayCode = BuildUniqueDelayCode($"{duration.Kod}_DELAY", rows.Select(x => x.Kod));
                dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
                {
                    Kod = fallbackDelayCode,
                    Nazev = $"{duration.Nazev} - zpozdeni",
                    Hodnota = 100 + order,
                    IsLocked = true,
                    SablonaVerze = schemaVersion,
                    KrokKey = duration.KrokKey,
                    KrokPoradi = order,
                    JeZpozdeni = true,
                    BarvaHex = null
                });
            }
        }
    }

    private static string BuildUniqueDelayCode(string baseCode, IEnumerable<string?> usedCodes)
    {
        var codeBase = string.IsNullOrWhiteSpace(baseCode) ? "STEP_DELAY" : baseCode.Trim();
        var used = new HashSet<string>(usedCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()), Ci);
        if (!used.Contains(codeBase))
        {
            return codeBase;
        }

        var index = 2;
        while (used.Contains($"{codeBase}_{index}"))
        {
            index += 1;
        }

        return $"{codeBase}_{index}";
    }
}
