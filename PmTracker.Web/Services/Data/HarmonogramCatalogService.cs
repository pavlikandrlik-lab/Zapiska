using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class HarmonogramCatalogService(
    PmTrackerDbContext dbContext,
    TimeProvider timeProvider) : IHarmonogramCatalogService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const int HarmonogramDelayColorPseudoRowId = 0;
    private const string HarmonogramDelayColorPseudoKod = "DELAY_COLOR";
    private const string DefaultDelayBarvaHex = "#DC2626";

    public Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(
        string id,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct = default)
        => BuildHarmonogramKrokyCiselnikDetailAsync(id, currentUser.IsSuperAdmin, ct);

    public async Task<CiselnikDetailViewModel> BuildHarmonogramKrokyCiselnikDetailAsync(
        string key,
        bool canChangeLockState,
        CancellationToken ct = default)
    {
        var schema = await LoadActiveHarmonogramSchemaAsync(ct);
        var rows = schema.Kroky
            .OrderBy(x => x.KrokIndex)
            .Select(step => new CiselnikRadekViewModel
            {
                Id = step.TrvaniTypId,
                Kod = step.Kod,
                Nazev = step.Nazev,
                IsLocked = false,
                CanChangeLockState = canChangeLockState,
                CanEdit = true,
                CanDelete = true,
                HodnotyNavic = [step.BarvaHex],
                HodnotyNavicRaw = [step.BarvaHex]
            })
            .ToList();

        rows.Add(new CiselnikRadekViewModel
        {
            Id = HarmonogramDelayColorPseudoRowId,
            Kod = HarmonogramDelayColorPseudoKod,
            Nazev = "Globalni barva skutecnosti",
            IsLocked = true,
            CanChangeLockState = canChangeLockState,
            CanEdit = true,
            CanDelete = false,
            HodnotyNavic = [schema.DelayBarvaHex],
            HodnotyNavicRaw = [schema.DelayBarvaHex]
        });

        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = "Harmonogramove kroky",
            CanCreate = true,
            CanChangeLockState = canChangeLockState,
            SloupceNavic = ["Barva"],
            IsHodnotaNavicSelect = false,
            IsHodnotaNavicRequired = true,
            Polozky = rows
        };
    }

    public async Task<int> CountHarmonogramCatalogRowsAsync(CancellationToken ct = default)
    {
        var schema = await LoadActiveHarmonogramSchemaAsync(ct);
        return Math.Max(0, schema.Kroky.Count + 1);
    }

    public async Task SaveHarmonogramStepRowAsync(
        SaveCiselnikRowCommand command,
        CancellationToken ct = default)
    {
        var rawColor = command.HodnotyNavic.FirstOrDefault()?.Trim();
        if (!IsValidHexColor(rawColor))
        {
            throw new InvalidOperationException("Barva kroku musi byt ve formatu #RRGGBB.");
        }

        var colorHex = rawColor!.ToUpperInvariant();
        var clone = await CloneActiveHarmonogramSchemaAsync(ct);
        var rows = clone.ClonedRows.ToList();
        var durationRows = rows.Where(x => !x.JeZpozdeni).OrderBy(x => x.KrokPoradi).ThenBy(x => x.Id).ToList();

        if (command.Id.GetValueOrDefault() == HarmonogramDelayColorPseudoRowId)
        {
            clone.NewSchema.DelayBarvaHex = colorHex;
            await FinalizeClonedHarmonogramSchemaAsync(clone.NewSchema, normalizeRows: false, ct);
            return;
        }

        var kod = (command.Kod ?? string.Empty).Trim();
        var nazev = (command.Nazev ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(kod))
        {
            throw new InvalidOperationException("Kod kroku harmonogramu je povinny.");
        }

        if (string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Nazev kroku harmonogramu je povinny.");
        }

        if (command.Id.HasValue)
        {
            if (!clone.ClonedBySourceId.TryGetValue(command.Id.Value, out var targetStep) || targetStep.JeZpozdeni)
            {
                throw new InvalidOperationException("Upravovat lze pouze hlavni kroky harmonogramu.");
            }

            var duplicateCodeExists = durationRows.Any(x => x.Id != targetStep.Id && Ci.Equals(x.Kod, kod));
            if (duplicateCodeExists)
            {
                throw new InvalidOperationException($"Krok s kodem '{kod}' uz existuje.");
            }

            targetStep.Kod = kod;
            targetStep.Nazev = nazev;
            targetStep.BarvaHex = colorHex;

            var pairedDelay = rows.FirstOrDefault(x => x.JeZpozdeni && x.KrokKey == targetStep.KrokKey);
            if (pairedDelay is not null && string.IsNullOrWhiteSpace(pairedDelay.Nazev))
            {
                pairedDelay.Nazev = $"{nazev} - zpozdeni";
            }
        }
        else
        {
            var duplicateCodeExists = durationRows.Any(x => Ci.Equals(x.Kod, kod));
            if (duplicateCodeExists)
            {
                throw new InvalidOperationException($"Krok s kodem '{kod}' uz existuje.");
            }

            var nextOrder = durationRows.Count == 0 ? 1 : durationRows.Max(x => x.KrokPoradi) + 1;
            var stepKey = Guid.NewGuid();
            var usedCodes = rows.Select(x => x.Kod).Where(x => !string.IsNullOrWhiteSpace(x));
            var delayKod = BuildUniqueDelayCode($"{kod}_DELAY", usedCodes);

            dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Kod = kod,
                Nazev = nazev,
                Hodnota = nextOrder,
                IsLocked = true,
                SablonaVerze = clone.NewSchema.Verze,
                KrokKey = stepKey,
                KrokPoradi = nextOrder,
                JeZpozdeni = false,
                BarvaHex = colorHex
            });

            dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Kod = delayKod,
                Nazev = $"{nazev} - zpozdeni",
                Hodnota = 100 + nextOrder,
                IsLocked = true,
                SablonaVerze = clone.NewSchema.Verze,
                KrokKey = stepKey,
                KrokPoradi = nextOrder,
                JeZpozdeni = true,
                BarvaHex = null
            });
        }

        await dbContext.SaveChangesAsync(ct);
        await FinalizeClonedHarmonogramSchemaAsync(clone.NewSchema, ct: ct);
    }

    public async Task DeleteHarmonogramStepRowAsync(
        DeleteCiselnikRowCommand command,
        CancellationToken ct = default)
    {
        if (command.Id == HarmonogramDelayColorPseudoRowId)
        {
            throw new InvalidOperationException("Globalni barvu zpozdeni nelze smazat.");
        }

        var clone = await CloneActiveHarmonogramSchemaAsync(ct);
        if (!clone.ClonedBySourceId.TryGetValue(command.Id, out var targetStep) || targetStep.JeZpozdeni)
        {
            throw new InvalidOperationException("Smazat lze pouze hlavni kroky harmonogramu.");
        }

        var remainingMainSteps = clone.ClonedRows.Count(x => !x.JeZpozdeni && x.Id != targetStep.Id);
        if (remainingMainSteps <= 0)
        {
            throw new InvalidOperationException("Harmonogram musi obsahovat alespon jeden krok.");
        }

        var pairedDelay = clone.ClonedRows.FirstOrDefault(x => x.JeZpozdeni && x.KrokKey == targetStep.KrokKey);
        dbContext.CiselnikHarmonogramTypu.Remove(targetStep);
        if (pairedDelay is not null)
        {
            dbContext.CiselnikHarmonogramTypu.Remove(pairedDelay);
        }

        await dbContext.SaveChangesAsync(ct);
        await FinalizeClonedHarmonogramSchemaAsync(clone.NewSchema, ct: ct);
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
            var delayType = delayRows.FirstOrDefault(x => x.KrokKey == duration.KrokKey)
                ?? delayRows.FirstOrDefault(x => x.KrokPoradi == duration.KrokPoradi);

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

    private static HarmonogramSchemaDefinition BuildFallbackSchemaDefinition(int version = 0, string? delayBarvaHex = null)
    {
        return new HarmonogramSchemaDefinition(
            version,
            NormalizeHexColor(delayBarvaHex, DefaultDelayBarvaHex),
            DefaultHarmonogramKroky
                .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                .ToList());
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

    private async Task<HarmonogramSchemaCloneResult> CloneActiveHarmonogramSchemaAsync(CancellationToken ct)
    {
        var sourceSchema = await dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Aktivni harmonogramova sablona neni dostupna.");

        var sourceRows = await dbContext.CiselnikHarmonogramTypu
            .AsNoTracking()
            .Where(x => x.SablonaVerze == sourceSchema.Verze)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.JeZpozdeni)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);
        if (sourceRows.Count == 0)
        {
            throw new InvalidOperationException("Aktivni harmonogramova sablona neobsahuje zadne kroky.");
        }

        var newSchema = new HarmonogramSablonaEntity
        {
            DelayBarvaHex = NormalizeHexColor(sourceSchema.DelayBarvaHex, DefaultDelayBarvaHex),
            IsAktivni = false,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            CreatedBy = null
        };
        dbContext.HarmonogramSablony.Add(newSchema);
        await dbContext.SaveChangesAsync(ct);

        var mapBySourceId = new Dictionary<int, HarmonogramTypEntity>();
        var clonedRows = new List<HarmonogramTypEntity>(sourceRows.Count);
        foreach (var sourceRow in sourceRows)
        {
            var clone = new HarmonogramTypEntity
            {
                Kod = sourceRow.Kod,
                Nazev = sourceRow.Nazev,
                Hodnota = sourceRow.Hodnota,
                IsLocked = sourceRow.IsLocked,
                SablonaVerze = newSchema.Verze,
                KrokKey = sourceRow.KrokKey,
                KrokPoradi = sourceRow.KrokPoradi,
                JeZpozdeni = sourceRow.JeZpozdeni,
                BarvaHex = sourceRow.BarvaHex
            };
            dbContext.CiselnikHarmonogramTypu.Add(clone);
            mapBySourceId[sourceRow.Id] = clone;
            clonedRows.Add(clone);
        }

        await dbContext.SaveChangesAsync(ct);

        return new HarmonogramSchemaCloneResult(sourceSchema, newSchema, mapBySourceId, clonedRows);
    }

    private async Task ActivateClonedHarmonogramSchemaAsync(HarmonogramSablonaEntity newSchema, CancellationToken ct)
    {
        var activeSchemas = await dbContext.HarmonogramSablony
            .Where(x => x.IsAktivni && x.Verze != newSchema.Verze)
            .ToListAsync(ct);
        foreach (var schema in activeSchemas)
        {
            schema.IsAktivni = false;
        }

        newSchema.IsAktivni = true;
    }

    private async Task FinalizeClonedHarmonogramSchemaAsync(
        HarmonogramSablonaEntity newSchema,
        bool normalizeRows = true,
        CancellationToken ct = default)
    {
        if (normalizeRows)
        {
            await NormalizeHarmonogramSchemaRowsAsync(newSchema.Verze, ct);
        }

        await ActivateClonedHarmonogramSchemaAsync(newSchema, ct);
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
        (10, "HS10_DURATION", "10. nasazeni do provozu", "#8B5CF6"),
        (11, "HS11_DURATION", "11. fakturace", "#D946EF")
    ];

    private static string ResolveDefaultStepColor(int stepIndex)
    {
        var matched = DefaultHarmonogramKroky.FirstOrDefault(step => step.Poradi == stepIndex);
        if (matched.Poradi > 0 && !string.IsNullOrWhiteSpace(matched.BarvaHex))
        {
            return matched.BarvaHex;
        }

        return DefaultHarmonogramKroky.Length > 0 ? DefaultHarmonogramKroky[0].BarvaHex : "#94A3B8";
    }
}
