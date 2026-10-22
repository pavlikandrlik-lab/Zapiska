using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Export;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Spec 2026-10-22 R5–R8, R11: seznamy záznamů pro detail jednání a pro procházení šipkami.
/// Jediný zdroj pravdy „co patří do jednání“ — tisk zápisu, detail jednání i šipky.
/// </summary>
public interface IRecordSequenceService
{
    /// <summary>
    /// Záznamy, které patří do jednání (= záznamy jeho zápisu), v pořadí zápisu. Pravidlo je
    /// <see cref="IExportRecordVisibilityEvaluator.IsVisibleForMeetingPrint"/> ke dni jednání
    /// a ke dni minulého jednání téhož projektu. Neexistující jednání = prázdný seznam.
    /// </summary>
    Task<IReadOnlyList<RecordSequenceEntry>> GetMeetingSequenceAsync(int jednaniId, CancellationToken ct = default);

    /// <summary>Aktivní záznamy projektu (bez stavu, nebo stav není konečný — jako filtr „Aktivní“) v pořadí zápisu.</summary>
    Task<IReadOnlyList<RecordSequenceEntry>> GetActiveSequenceAsync(int projektId, CancellationToken ct = default);

    /// <summary>
    /// Seznam podle parametru „seznam“ pro záznam projektu <paramref name="projektId"/>.
    /// Null = jednání jiného projektu nebo neexistující — stránka je pak bez šipek.
    /// </summary>
    Task<IReadOnlyList<RecordSequenceEntry>?> ResolveAsync(RecordSequenceRequest request, int projektId, CancellationToken ct = default);
}

public sealed class RecordSequenceService(
    PmTrackerDbContext dbContext,
    IExportRecordVisibilityEvaluator visibilityEvaluator) : IRecordSequenceService
{
    public async Task<IReadOnlyList<RecordSequenceEntry>> GetMeetingSequenceAsync(int jednaniId, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == jednaniId)
            .Select(x => new { x.ProjektId, x.CisloJednani, x.DatumPlanovane })
            .FirstOrDefaultAsync(ct);
        if (meeting is null)
        {
            return [];
        }

        // Minulé jednání = nejbližší nižší číslo v projektu — stejně jako tisk zápisu.
        var previousMeetingDate = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == meeting.ProjektId && x.CisloJednani < meeting.CisloJednani)
            .OrderByDescending(x => x.CisloJednani)
            .Select(x => (DateTime?)x.DatumPlanovane)
            .FirstOrDefaultAsync(ct);

        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == meeting.ProjektId)
            .ToListAsync(ct);
        var recordIds = records.Select(x => x.Id).ToArray();
        var taskStates = await dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        // Pořadí historie jako v tisku (nejnovější platnost napřed) — výpočet stavu ke dni na něm závisí.
        var historyByRecord = (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderByDescending(x => x.PlatiOd)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ZaznamHistorieStavuZaznamuEntity>)group.ToList());

        var members = records
            .Where(record => visibilityEvaluator.IsVisibleForMeetingPrint(
                record,
                taskStates,
                historyByRecord.GetValueOrDefault(record.Id) ?? Array.Empty<ZaznamHistorieStavuZaznamuEntity>(),
                meeting.DatumPlanovane.Date,
                previousMeetingDate?.Date))
            .ToList();
        return await OrderAsync(meeting.ProjektId, members, ct);
    }

    public async Task<IReadOnlyList<RecordSequenceEntry>> GetActiveSequenceAsync(int projektId, CancellationToken ct = default)
    {
        var finalStateIds = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.IsFinal)
            .Select(x => x.Id)
            .ToArrayAsync(ct);
        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .Where(x => !x.StavUkoluId.HasValue || !finalStateIds.Contains(x.StavUkoluId.Value))
            .ToListAsync(ct);
        return await OrderAsync(projektId, records, ct);
    }

    public async Task<IReadOnlyList<RecordSequenceEntry>?> ResolveAsync(
        RecordSequenceRequest request, int projektId, CancellationToken ct = default)
    {
        if (request.JednaniId is not int jednaniId)
        {
            return await GetActiveSequenceAsync(projektId, ct);
        }

        // Jednání jiného projektu (nebo neexistující) se ignoruje — stránka bez šipek (spec §6).
        var meetingProjectId = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == jednaniId)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct);
        return meetingProjectId == projektId
            ? await GetMeetingSequenceAsync(jednaniId, ct)
            : null;
    }

    private async Task<IReadOnlyList<RecordSequenceEntry>> OrderAsync(
        int projektId, IReadOnlyList<ProjektovyZaznamEntity> records, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var subsystemIds = records.Select(x => x.SubsystemId).Distinct().ToArray();
        var subsystems = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => subsystemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var orderBySubsystemId = await ProjectSubsystemOrderingQuery.LoadActiveOrderBySubsystemIdAsync(dbContext, projektId, subsystemIds, ct);
        var categories = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);

        // Klíč řazení = stejné hodnoty jako řádek tisku (ExportRecordProjectionBuilder).
        MinutesOrderKey Key(ProjektovyZaznamEntity record)
        {
            var subsystem = subsystems.GetValueOrDefault(record.SubsystemId);
            var hasOrder = orderBySubsystemId.TryGetValue(record.SubsystemId, out var poradi);
            return new MinutesOrderKey(
                subsystem?.Nazev ?? "-",
                subsystem?.Kod ?? "-",
                hasOrder ? poradi : 0,
                hasOrder,
                categories.GetValueOrDefault(record.KategorieId)?.Nazev ?? "-",
                RecordDisplayOrdering.VisibleNumberPartA(record.CisloViditelneA, record.CisloZaznamu),
                RecordDisplayOrdering.VisibleNumberPartB(record.CisloViditelneTyp, record.CisloViditelneB),
                record.CisloZaznamu);
        }

        return MinutesRecordOrdering.InMinutesOrder(records, Key)
            .Select(record => new RecordSequenceEntry(
                record.Id,
                string.IsNullOrWhiteSpace(record.CisloViditelne)
                    ? record.CisloZaznamu.ToString(CultureInfo.InvariantCulture)
                    : record.CisloViditelne.Trim(),
                record.Nazev))
            .ToList();
    }
}
