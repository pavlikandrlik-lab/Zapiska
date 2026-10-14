using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class RecordProposalService
{
    public async Task<int?> ApproveProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadPendingProposalForDecisionAsync(command, currentUser, ct);
        var oldProposalSnapshot = ProposalAuditSnapshot.FromEntity(proposal);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        // Review finding C-7: enqueue harvestu musí probíhat PO commit, ne uvnitř
        // strategy.ExecuteAsync closure, protože retry strategie by firework enqueuenula
        // vícekrát a rollback by vedl k enqueue bez reálné DB mutace.
        int? harvestRecordIdToEnqueue = null;
        // Spec 2026-10-14 R2: po přepnutí na automatiku automat hned doplní kroky z navázaných
        // vyjádření (harvest sám automat spouští jen při NOVÉM vyjádření). Po commitu, jako harvest.
        int? automatRecordIdAfterCommit = null;

        try
        {
            var approvedRecordId = await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // Reset per pokus — retry by jinak mohl enqueue-ovat ze starého pokusu.
                harvestRecordIdToEnqueue = null;
                automatRecordIdAfterCommit = null;
                int? localApprovedRecordId = null;

                if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
                {
                    var payload = DeserializePayload(proposal.PayloadJson);
                    var createPayload = payload.CreateRecord
                        ?? throw new InvalidOperationException("Payload návrhu založení záznamu je neplatný.");
                    var saveCommand = _payloadMapper.BuildSaveCommand(createPayload);
                    localApprovedRecordId = await _recordService.SaveRecordAsync(saveCommand, currentUser, ct);

                    if (localApprovedRecordId.HasValue
                        && createPayload.HarmonogramVazby.Count > 0)
                    {
                        var shouldEnqueue = await ApplyApprovedCreateProposalAuxiliariesAsync(
                            localApprovedRecordId.Value,
                            createPayload,
                            currentUser,
                            ct);
                        if (shouldEnqueue)
                        {
                            harvestRecordIdToEnqueue = localApprovedRecordId;
                        }
                    }
                }
                else if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase))
                {
                    // Task 5 (2026-10-12): přepínač „Automatické vyplňování harmonogramu" se po
                    // schválení provede. Přepnutí na Auto spustí po commitu automat a naplánuje
                    // harvest (review fix M2 2026-10-12; spec 2026-10-14 R2 — stejně jako SaveRecord).
                    if (await ApplyApprovedScheduleProposalAsync(proposal, currentUser, ct))
                    {
                        automatRecordIdAfterCommit = proposal.ZaznamId;
                        harvestRecordIdToEnqueue = proposal.ZaznamId;
                    }
                    localApprovedRecordId = proposal.ZaznamId;
                }
                else
                {
                    throw new InvalidOperationException("Neznámý typ návrhu.");
                }

                proposal.Stav = RecordProposalStateCodes.Approved;
                proposal.DecidedByOsobaId = currentUser.OsobaId;
                proposal.DecidedAt = _timeProvider.GetUtcNow().UtcDateTime;
                proposal.ApprovedRecordId = localApprovedRecordId;
                await _dbContext.SaveChangesAsync(ct);
                _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Approve,
                    AuditEntityType.RecordProposal,
                    proposal.Id.ToString(CultureInfo.InvariantCulture),
                    oldProposalSnapshot,
                    ProposalAuditSnapshot.FromEntity(proposal)));
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return localApprovedRecordId;
            });

            if (automatRecordIdAfterCommit.HasValue)
            {
                await _skutecnostSync.SyncZaznamBestEffortAsync(automatRecordIdAfterCommit.Value, ct);
            }

            // Post-commit enqueue — bezpečné vůči retry i rollback.
            // T7 — schválení CREATE_RECORD návrhu s externími vazbami (spec §8.2).
            if (harvestRecordIdToEnqueue.HasValue)
            {
                await _harvestScheduler.ScheduleHarvestForRecordAsync(
                    harvestRecordIdToEnqueue.Value,
                    ct,
                    PmTracker.Web.Services.ServiceDesk.SdReactiveSource.ProposalApprove);
            }

            return approvedRecordId;
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
            throw new InvalidOperationException("Pro rozhodnutí o návrhu je třeba oprávnění proposals.accept na projektu.");
        }

        var proposal = await LoadProposalAsync(command.ProjektId, command.ProposalId, ct);
        if (!string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Rozhodnout lze jen o návrhu ve stavu čeká na rozhodnutí.");
        }

        return proposal;
    }

    /// <summary>
    /// Task 5 (2026-10-12): vrací, zda návrh přepnul záznam na automatiku — pak se po commitu
    /// spustí automat a naplánuje ServiceDesk harvest (spec 2026-10-14 R2).
    /// </summary>
    private async Task<bool> ApplyApprovedScheduleProposalAsync(ZaznamNavrhEntity proposal, CurrentUserContextViewModel currentUser, CancellationToken ct)
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
        var oldDeadline = record.DatumUkonceni.Date;

        // Datum-model: aplikuj návrh schedule na krok rows (plán + skutečnost datumy).
        if (schedulePayload.ChangesTermDeadline)
        {
            record.DatumUkonceni = schedulePayload.TerminUkonceni.Date;
        }

        // Task 4 (2026-10-12): dřív schválení historii termínu nezapisovalo vůbec (zapisoval jen
        // RecordService.SaveRecord). Zapiš jen když se termín skutečně změnil.
        if (record.DatumUkonceni.Date != oldDeadline)
        {
            _dbContext.ZaznamHistorieTerminu.Add(new ZaznamHistorieTerminuEntity
            {
                ZaznamId = record.Id,
                PuvodniDatum = oldDeadline,
                NoveDatum = record.DatumUkonceni.Date,
                DatumZmeny = _timeProvider.GetLocalNow().LocalDateTime,
                Duvod = string.IsNullOrWhiteSpace(schedulePayload.DuvodZmeny)
                    ? $"Schválený návrh #{proposal.Id}"
                    : schedulePayload.DuvodZmeny.Trim()
            });
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var krokRows = await _dbContext.ZaznamHarmonogramKroky
            .Where(k => k.ZaznamId == record.Id)
            .ToListAsync(ct);
        var krokByPoradi = krokRows.GroupBy(k => (int)k.Poradi).ToDictionary(g => g.Key, g => g.First());

        ZaznamHarmonogramKrokEntity GetOrCreateKrok(int poradi)
        {
            if (krokByPoradi.TryGetValue(poradi, out var r))
            {
                return r;
            }
            r = new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = record.Id,
                Poradi = (byte)poradi,
                // R3: kroky 2/5/8/9 vznikají jako ruční, ostatní jako automatické.
                SkutecnostRezim = ScheduleActualApplier.VychoziRezim(poradi),
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = nowUtc
            };
            _dbContext.ZaznamHarmonogramKroky.Add(r);
            krokByPoradi[poradi] = r;
            return r;
        }

        if (schedulePayload.ChangesSchedulePlan)
        {
            foreach (var pv in schedulePayload.PlannedHarmonogramHodnoty.Where(x => x.Poradi is >= 1 and <= 10))
            {
                var row = GetOrCreateKrok(pv.Poradi);
                row.PlanDatum = pv.PlanDatum?.Date;
                row.UpdatedAt = nowUtc;
            }
        }

        // Spec 2026-10-14: skutečnost a režim přes stejnou komponentu jako uložení editoru
        // (ScheduleActualApplier). Uživatel 2026-10-08: přepínač v návrhu se po schválení opravdu
        // provede. Fix round 1 (2026-10-12): režim se mění jen tehdy, když ho návrh opravdu mění
        // (ChangesHarmonogramRezim) — editor posílá HarmonogramRezim vždy a bez téhle brány by
        // schválení přepsalo režim, který se mezitím změnil přímou editací.
        var cilovyRezim = schedulePayload.ChangesHarmonogramRezim
            ? HarmonogramAutoFillRule.Normalize(schedulePayload.HarmonogramRezim)
            : null;
        IReadOnlyList<ScheduleActualApplier.SkutecnostZmena> zmeny = schedulePayload.ChangesScheduleActual
            ? schedulePayload.ActualHarmonogramHodnoty
                .Select(av => new ScheduleActualApplier.SkutecnostZmena(av.Poradi, av.SkutecnostDatum))
                .ToList()
            : [];
        var vysledek = ScheduleActualApplier.Apply(
            krokByPoradi.Values.ToList(), GetOrCreateKrok, zmeny, cilovyRezim, nowUtc);
        var switchedToAuto = vysledek.PrepnutoNaAuto;

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
        await _dbContext.SaveChangesAsync(ct);
        return switchedToAuto;
    }

    /// <summary>
    /// Plán D (schéma 3 — CREATE_RECORD): po vytvoření záznamu aplikuje vedlejší efekty
    /// z payloadu — pre-bound vazby bublin na kroky (skutečnost návrh založení nenese, HideActual).
    /// Externí odkazy jsou namapovány přes <c>ExterniOdkazIndex</c> = index v pořadí
    /// vložení (query seřazená ASC dle Id).
    ///
    /// Review finding C-7: harvest enqueue je vytažen ven; metoda vrací bool, zda má
    /// caller enqueue-ovat PO commitu.
    /// </summary>
    private async Task<bool> ApplyApprovedCreateProposalAuxiliariesAsync(
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
                    Poradi = (byte)v.Poradi,
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

        // Review finding C-7: enqueue se přesunulo do ApproveProposalAsync PO commit.
        // Zde pouze signalizujeme callerovi, zda má enqueue-ovat.
        return externiOdkazyIds.Count > 0;
    }

}
