using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;

namespace PmTracker.Web.Services;

public sealed partial class RecordProposalService
{
    public async Task<int?> ApproveProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                int? approvedRecordId = null;

                if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
                {
                    var payload = DeserializePayload(proposal.PayloadJson);
                    var createPayload = payload.CreateRecord
                        ?? throw new InvalidOperationException("Payload návrhu založení záznamu je neplatný.");
                    var saveCommand = _payloadMapper.BuildSaveCommand(createPayload);
                    approvedRecordId = await _recordService.SaveRecordAsync(saveCommand, currentUser, ct);

                    if (approvedRecordId.HasValue
                        && (createPayload.HarmonogramVazby.Count > 0 || createPayload.ManualActualKroky.Count > 0))
                    {
                        await ApplyApprovedCreateProposalAuxiliariesAsync(
                            approvedRecordId.Value,
                            createPayload,
                            currentUser,
                            ct);
                    }
                }
                else if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase))
                {
                    await ApplyApprovedScheduleProposalAsync(proposal, currentUser, ct);
                    approvedRecordId = proposal.ZaznamId;
                }
                else
                {
                    throw new InvalidOperationException("Neznámý typ návrhu.");
                }

                proposal.Stav = RecordProposalStateCodes.Approved;
                proposal.DecidedByOsobaId = currentUser.OsobaId;
                proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
                proposal.ApprovedRecordId = approvedRecordId;
                await _dbContext.SaveChangesAsync(ct);
                _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Approve,
                    AuditEntityType.RecordProposal,
                    proposal.Id.ToString(CultureInfo.InvariantCulture),
                    oldProposalSnapshot,
                    ProposalAuditSnapshot.FromEntity(proposal)));
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return approvedRecordId;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "Návrh byl mezitím změněn jiným uživatelem. Obnovte stránku a zkuste znovu.");
        }
    }

    public async Task RejectProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            proposal.Stav = RecordProposalStateCodes.Rejected;
            proposal.DecidedByOsobaId = currentUser.OsobaId;
            proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(ct);
            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Reject,
                AuditEntityType.RecordProposal,
                proposal.Id.ToString(CultureInfo.InvariantCulture),
                oldProposalSnapshot,
                ProposalAuditSnapshot.FromEntity(proposal)));
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task RejectAndTakeOverCreateProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        if (!string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Převzetí do formuláře je dostupné jen pro návrh založení záznamu.");
        }

        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            proposal.Stav = RecordProposalStateCodes.Rejected;
            proposal.DecidedByOsobaId = currentUser.OsobaId;
            proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(ct);
            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Reject,
                AuditEntityType.RecordProposal,
                proposal.Id.ToString(CultureInfo.InvariantCulture),
                oldProposalSnapshot,
                ProposalAuditSnapshot.FromEntity(proposal, "takeover")));
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task RejectAndEditProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            proposal.Stav = RecordProposalStateCodes.Rejected;
            proposal.DecidedByOsobaId = currentUser.OsobaId;
            proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync(ct);
            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Reject,
                AuditEntityType.RecordProposal,
                proposal.Id.ToString(CultureInfo.InvariantCulture),
                oldProposalSnapshot,
                ProposalAuditSnapshot.FromEntity(proposal, "edit")));
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    // --- Private helpers used only by DecisionCommands ---

    private async Task<ZaznamNavrhEntity> LoadPendingProposalForDecisionAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        if (!await _authorizationPolicy.CanDecideProjectProposalAsync(command.ProjektId, currentUser, ct))
        {
            throw new InvalidOperationException("O návrzích může rozhodovat jen projektový manažer nebo administrátor projektu.");
        }

        var proposal = await LoadProposalAsync(command.ProjektId, command.ProposalId, ct);
        if (!string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Rozhodnout lze jen o návrhu ve stavu čeká na rozhodnutí.");
        }

        return proposal;
    }

    private async Task ApplyApprovedScheduleProposalAsync(ZaznamNavrhEntity proposal, CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        if (!proposal.ZaznamId.HasValue)
        {
            throw new InvalidOperationException("Návrh změny harmonogramu není navázán na záznam.");
        }

        var payload = DeserializePayload(proposal.PayloadJson);
        var schedulePayload = payload.SchedulePlan
            ?? throw new InvalidOperationException("Payload návrhu harmonogramu je neplatný.");
        var record = await _dbContext.ProjektoveZaznamy
            .FirstOrDefaultAsync(x => x.Id == proposal.ZaznamId.Value && x.ProjektId == proposal.ProjektId, ct)
            ?? throw new InvalidOperationException($"Záznam {proposal.ZaznamId.Value} nebyl nalezen.");
        var oldRecordSnapshot = RecordAuditSnapshot.FromEntity(record);

        var schema = await _harmonogramService.GetSchemaForRecordAsync(record, ct);
        var scheduleTypeDefinitions = _harmonogramService.BuildRecordScheduleTypeDefinitions(schema);
        var plannedTypeIds = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();
        var actualTypeIds = scheduleTypeDefinitions
            .Select(x => x.DelayTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();
        var allowedTypeIds = plannedTypeIds.Concat(actualTypeIds).ToHashSet();
        var submittedValues = (schedulePayload.ChangesSchedulePlan
                ? schedulePayload.PlannedHarmonogramHodnoty.Where(value => plannedTypeIds.Contains(value.TypId))
                : Enumerable.Empty<SaveRecordHarmonogramValueCommand>())
            .Concat(
                schedulePayload.ChangesScheduleActual
                    ? schedulePayload.ActualHarmonogramHodnoty.Where(value => actualTypeIds.Contains(value.TypId))
                    : Enumerable.Empty<SaveRecordHarmonogramValueCommand>())
            .GroupBy(value => value.TypId)
            .Select(group => new SaveRecordHarmonogramValueCommand
            {
                TypId = group.Key,
                Hodnota = plannedTypeIds.Contains(group.Key)
                    ? Math.Max(0, group.Last().Hodnota)
                    : group.Last().Hodnota
            })
            .ToList();
        var existingRows = await _dbContext.ZaznamHarmonogramHodnoty
            .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
            .ToListAsync(ct);
        var oldScheduleSnapshot = existingRows.Count == 0
            ? null
            : RecordScheduleAuditSnapshot.FromEntities(record.Id, existingRows);
        var normalizedValues = submittedValues
            .Where(x => x.Hodnota > 0 || actualTypeIds.Contains(x.TypId))
            .ToDictionary(x => x.TypId, x => x.Hodnota);

        if (schedulePayload.ChangesTermDeadline)
        {
            record.DatumUkonceni = schedulePayload.TerminUkonceni.Date;
        }

        // Plán D: aplikace ManualActualKroky[] — přepočet absolutní datum -> odchylka
        // proti plánovanému konci kroku. Počítá se s plánem PO aplikaci navrhovaných
        // změn trvání (submittedValues), takže ruční datum z návrhu ctí posun plánu.
        if (schedulePayload.ManualActualKroky.Count > 0)
        {
            var manualOverrides = ComputeManualActualOverrides(
                schedulePayload.ManualActualKroky,
                schema,
                record.DatumZalozeni,
                plannedTypeIds,
                submittedValues);
            foreach (var ov in manualOverrides)
            {
                // Přepiš přípravnou hodnotu (schedulePayload.ActualHarmonogramHodnoty) tím,
                // co dal user v ManualActualKroky. DELAY hodnoty mohou být i záporné.
                normalizedValues[ov.DelayTypId] = ov.OdchylkaDni;
            }
        }

        foreach (var row in existingRows)
        {
            if (normalizedValues.TryGetValue(row.TypId, out var updatedValue))
            {
                row.HodnotaInt = updatedValue;
                row.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                normalizedValues.Remove(row.TypId);
            }
            else if ((plannedTypeIds.Contains(row.TypId) && schedulePayload.ChangesSchedulePlan)
                || (actualTypeIds.Contains(row.TypId) && schedulePayload.ChangesScheduleActual))
            {
                _dbContext.ZaznamHarmonogramHodnoty.Remove(row);
            }
        }

        foreach (var plannedValue in normalizedValues)
        {
            _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = record.Id,
                TypId = plannedValue.Key,
                HodnotaInt = plannedValue.Value,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        if (schedulePayload.ChangesTermDeadline || schedulePayload.ChangesSchedulePlan)
        {
            await _priorityMatrixRebuildService.RebuildForRecordAsync(record.Id, ct);
        }
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.Record,
            record.Id.ToString(CultureInfo.InvariantCulture),
            oldRecordSnapshot,
            RecordAuditSnapshot.FromEntity(record)));

        var newScheduleRows = await _dbContext.ZaznamHarmonogramHodnoty
            .AsNoTracking()
            .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
            .ToListAsync(ct);
        var newScheduleSnapshot = newScheduleRows.Count == 0
            ? null
            : RecordScheduleAuditSnapshot.FromEntities(record.Id, newScheduleRows);
        if (oldScheduleSnapshot is not null || newScheduleSnapshot is not null)
        {
            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                oldScheduleSnapshot is null ? AuditActionType.Create : AuditActionType.Update,
                AuditEntityType.RecordSchedule,
                record.Id.ToString(CultureInfo.InvariantCulture),
                oldScheduleSnapshot,
                newScheduleSnapshot));
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Plán D (schéma 3 — CREATE_RECORD): po vytvoření záznamu aplikuje vedlejší efekty
    /// z payloadu — ruční skutečnosti kroků a pre-bound vazby bublin na kroky.
    /// Externí odkazy jsou namapovány přes <c>ExterniOdkazIndex</c> = index v pořadí
    /// vložení (query seřazená ASC dle Id).
    /// </summary>
    private async Task ApplyApprovedCreateProposalAuxiliariesAsync(
        int newRecordId,
        CreateRecordProposalPayload createPayload,
        CurrentUserContextViewModel currentUser,
        CancellationToken ct)
    {
        var externiOdkazyIds = await _dbContext.ZaznamExterniOdkazy
            .AsNoTracking()
            .Where(x => x.ZaznamId == newRecordId)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(ct);

        // HarmonogramVazby → zaznam_harmonogram_vyjadreni_vazba + DELAY upsert
        if (createPayload.HarmonogramVazby.Count > 0)
        {
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var v in createPayload.HarmonogramVazby)
            {
                if (v.ExterniOdkazIndex < 0 || v.ExterniOdkazIndex >= externiOdkazyIds.Count)
                {
                    throw new InvalidOperationException(
                        $"Vazba vyjádření odkazuje na externí index {v.ExterniOdkazIndex}, který neexistuje v nově vytvořených externích odkazech.");
                }

                var externiOdkazId = externiOdkazyIds[v.ExterniOdkazIndex];
                _dbContext.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
                {
                    ZaznamId = newRecordId,
                    KrokKey = v.KrokKey,
                    ExterniOdkazId = externiOdkazId,
                    HotVyjadreniId = v.HotVyjadreniId,
                    DatumVyjadreni = v.DatumVyjadreni.UtcDateTime,
                    Source = (byte)VazbaSource.Manual,
                    Stav = (byte)VazbaStav.Active,
                    CreatedAt = nowUtc,
                    CreatedByOsobaId = currentUser.OsobaId
                });
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        // ManualActualKroky → DELAY upsert (odchylka dnů)
        if (createPayload.ManualActualKroky.Count > 0)
        {
            var record = await _dbContext.ProjektoveZaznamy
                .FirstOrDefaultAsync(x => x.Id == newRecordId, ct)
                ?? throw new InvalidOperationException($"Nově vytvořený záznam {newRecordId} nebyl nalezen.");
            var schema = await _harmonogramService.GetSchemaForRecordAsync(record, ct);
            var plannedTypeIds = schema.Kroky
                .Select(x => x.TrvaniTypId)
                .Where(x => x > 0)
                .ToHashSet();

            var overrides = ComputeManualActualOverrides(
                createPayload.ManualActualKroky,
                schema,
                record.DatumZalozeni,
                plannedTypeIds,
                Array.Empty<SaveRecordHarmonogramValueCommand>());

            var existingByTypId = await _dbContext.ZaznamHarmonogramHodnoty
                .Where(x => x.ZaznamId == newRecordId && overrides.Select(o => o.DelayTypId).Contains(x.TypId))
                .ToDictionaryAsync(x => x.TypId, ct);

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var ov in overrides)
            {
                if (existingByTypId.TryGetValue(ov.DelayTypId, out var existing))
                {
                    existing.HodnotaInt = ov.OdchylkaDni;
                    existing.UpdatedAt = nowUtc;
                }
                else
                {
                    _dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
                    {
                        ZaznamId = newRecordId,
                        TypId = ov.DelayTypId,
                        HodnotaInt = ov.OdchylkaDni,
                        UpdatedAt = nowUtc
                    });
                }
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        // Fire-and-forget harvest pro všechny externí odkazy — pokusí se
        // doplnit další vyjádření, která mezitím přibyla.
        if (externiOdkazyIds.Count > 0)
        {
            await _harvestScheduler.ScheduleHarvestForRecordAsync(newRecordId, ct);
        }
    }

    /// <summary>
    /// Plán D — sestaví KrokKey -&gt; Poradi a KrokKey -&gt; DelayTypeId mapování pro daný
    /// záznam a spočítá cílové odchylky z <see cref="ManualActualKrokDto"/> payloadu.
    /// Plánovaná timeline se počítá s kombinací stávajících hodnot a návrhem změněných
    /// trvání (<paramref name="submittedValues"/>), takže ruční datum respektuje plán
    /// PO schválení návrhu.
    /// </summary>
    private IReadOnlyList<ManualActualKrokApplier.ManualActualKrokApplied> ComputeManualActualOverrides(
        IReadOnlyList<ManualActualKrokDto> manualKroky,
        HarmonogramSchemaDefinition schema,
        DateTime datumZalozeni,
        IReadOnlySet<int> plannedTypeIds,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues)
    {
        if (manualKroky.Count == 0)
        {
            return Array.Empty<ManualActualKrokApplier.ManualActualKrokApplied>();
        }

        var krokKeyMeta = _dbContext.CiselnikHarmonogramTypu
            .AsNoTracking()
            .Where(x => x.SablonaVerze == schema.Verze)
            .Select(x => new { x.KrokKey, x.KrokPoradi, x.JeZpozdeni, x.Id })
            .ToList();

        var krokKeyToPoradi = krokKeyMeta
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.First().KrokPoradi);
        var delayTypIdByKrokKey = krokKeyMeta
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokKey)
            .ToDictionary(g => g.Key, g => g.First().Id);

        // Fallback: pokud delay řádek má jiný KrokKey než duration (legacy data), spáruj přes KrokPoradi
        var delayByPoradi = krokKeyMeta
            .Where(x => x.JeZpozdeni)
            .GroupBy(x => x.KrokPoradi)
            .ToDictionary(g => g.Key, g => g.First().Id);
        foreach (var mk in manualKroky)
        {
            if (delayTypIdByKrokKey.ContainsKey(mk.KrokKey)) continue;
            if (!krokKeyToPoradi.TryGetValue(mk.KrokKey, out var poradi)) continue;
            if (delayByPoradi.TryGetValue(poradi, out var fallbackDelayId))
            {
                delayTypIdByKrokKey[mk.KrokKey] = fallbackDelayId;
            }
        }

        // Sestavit efektivní hodnoty trvání pro timeline: výchozí schéma + submitted override
        var submittedByType = submittedValues
            .Where(x => plannedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .ToDictionary(g => g.Key, g => Math.Max(0, g.Last().Hodnota));
        var existingDurations = _dbContext.ZaznamHarmonogramHodnoty
            .AsNoTracking()
            .Where(x => plannedTypeIds.Contains(x.TypId))
            .Select(x => new { x.TypId, x.HodnotaInt })
            .ToList()
            .ToDictionary(x => x.TypId, x => x.HodnotaInt);
        foreach (var kv in submittedByType)
        {
            existingDurations[kv.Key] = kv.Value;
        }

        var vypocet = _harmonogramService.BuildHarmonogramVypocetPublic(
            datumZalozeni,
            schema.Kroky,
            existingDurations);

        return ManualActualKrokApplier.Compute(
            manualKroky,
            vypocet,
            krokKeyToPoradi,
            delayTypIdByKrokKey);
    }
}
