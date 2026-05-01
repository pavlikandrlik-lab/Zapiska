using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;

namespace PmTracker.Web.Services;

public sealed partial class RecordProposalService
{
    public async Task SubmitCreateRecordProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (command.Id.HasValue)
        {
            throw new InvalidOperationException("Návrh založení záznamu nesmí obsahovat existující ID záznamu.");
        }

        var access = await _authorizationPolicy.EvaluateProjectAccessAsync(command.ProjektId, currentUser, ct);
        if (access.CreatableSubsystemIds.Count == 0)
        {
            throw new InvalidOperationException("Návrh založení záznamu může vytvořit jen vedoucí subsystému nebo jeho zástupce.");
        }

        var subsystemId = await ResolveProjectSubsystemIdAsync(command.ProjektId, command.Subsystem, ct);
        if (!subsystemId.HasValue || !access.CreatableSubsystemIds.Contains(subsystemId.Value))
        {
            throw new InvalidOperationException("Můžete navrhovat záznam pouze pro subsystém, kde jste vedoucí nebo zástupce vedoucího.");
        }

        ValidateCommonProposalInput(command);
        await EnsureCreateProposalMeetingSelectionAsync(command, ct);

        var payload = _payloadMapper.BuildCreatePayload(command);
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var entity = new ZaznamNavrhEntity
            {
                ProjektId = command.ProjektId,
                SubsystemId = subsystemId.Value,
                TypNavrhu = RecordProposalTypeCodes.CreateRecord,
                Stav = RecordProposalStateCodes.Pending,
                PayloadJson = JsonSerializer.Serialize(payload),
                CreatedByOsobaId = currentUser.OsobaId,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            _dbContext.ZaznamNavrhy.Add(entity);
            await _dbContext.SaveChangesAsync(ct);
            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.RecordProposal,
                entity.Id.ToString(CultureInfo.InvariantCulture),
                null,
                ProposalAuditSnapshot.FromEntity(entity)));
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public async Task SubmitScheduleProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!command.Id.HasValue || command.Id.Value <= 0)
        {
            throw new InvalidOperationException("Návrh změny termínu a harmonogramu vyžaduje existující záznam.");
        }

        var record = await _dbContext.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
            ?? throw new InvalidOperationException($"Záznam {command.Id.Value} nebyl nalezen.");
        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        var access = await _authorizationPolicy.EvaluateRecordAccessAsync(command.ProjektId, command.Id.Value, currentUser, ct);
        if (!access.CanCreateScheduleProposal)
        {
            throw new InvalidOperationException("Návrh změny termínu a harmonogramu může vytvořit jen vedoucí relevantního subsystému nebo jeho zástupce.");
        }

        ValidateScheduleProposalInput(command, record);

        // Phase 8 (DESIGN-7-C + 7-D, 2026-05-01): max 1 Pending invariant + auto-supersede.
        // Pre-check existing Pending SCHEDULE_PLAN_CHANGE pro tento záznam. Při novém submitu:
        //   - Vlastní Pending + user má proposals.edit.own → auto-supersede starého (Stav=SUPERSEDED)
        //   - Cizí Pending + user má proposals.edit.any → admin override supersede + audit
        //   - Jinak → throw (pending má prioritu, vyžaduj rozhodnutí)
        // FIX 2026-05-01 (round 3 #20): AsNoTracking() — read-only validation, žádný tracker pollution.
        // Tracked entity by později kolidovala s následným re-load (line 173) v supersede branch.
        var existingPending = await _dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(n => n.ZaznamId == record.Id
                     && n.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange
                     && n.Stav == RecordProposalStateCodes.Pending)
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefaultAsync(ct);

        int? supersededByPlaceholderProposalId = null;

        if (existingPending is not null)
        {
            // FIX 2026-05-01 (round 2 #10): cycle detection — Pending návrh nesmí mít SupersededByProposalId
            // (defense-in-depth proti DB pollution / manuálním zásahům, kdy by se mohl vytvořit cykl
            // A.Stav=Pending + A.SupersededByProposalId=B → audit chain inconsistency).
            if (existingPending.SupersededByProposalId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Inconsistent state: Pending návrh #{existingPending.Id} má SupersededByProposalId=" +
                    $"#{existingPending.SupersededByProposalId.Value}. Stav je porušený, " +
                    "vyžádej manuální opravu před novým submitem.");
            }

            var isOwn = existingPending.CreatedByOsobaId == currentUser.OsobaId;
            var canEditOwn = currentUser.Authorization?.HasPermission(PermissionKeys.ProposalsEditOwn, command.ProjektId) ?? false;
            var canEditAny = currentUser.Authorization?.HasPermission(PermissionKeys.ProposalsEditAny, command.ProjektId) ?? false;

            if ((isOwn && canEditOwn) || canEditAny)
            {
                // Mark for auto-supersede (commit po vytvoření newProposal.Id níže).
                supersededByPlaceholderProposalId = existingPending.Id;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Záznam má pending návrh #{existingPending.Id}. Vyžádej zamítnutí, nebo počkej na rozhodnutí.");
            }
        }

        // Phase 6 (DESIGN-5-A + 7-A, 2026-05-01): odmítnout auto-fillované DELAY kroky.
        // Auto rezim ↔ návrh = mutuálně výlučné stavy. Návrh smí obsahovat jen DURATION
        // (planned) změny + manuální DELAY pro kroky 2/5/8/9.
        await ValidateAutoStepsNotInScheduleProposalAsync(command, record, ct);

        var scheduleTypeDefinitions = await ResolveScheduleTypeDefinitionsAsync(record, ct);
        var existingScheduleValues = await LoadExistingScheduleValuesAsync(record.Id, scheduleTypeDefinitions, ct);
        var plannedTypeIds = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var actualTypeIds = scheduleTypeDefinitions
            .Select(x => x.DelayTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var payload = _payloadMapper.BuildSchedulePayload(
            command,
            plannedTypeIds,
            actualTypeIds,
            record.DatumUkonceni,
            existingScheduleValues);
        var schedulePayload = payload.SchedulePlan;
        if (schedulePayload is null
            || (!schedulePayload.ChangesTermDeadline
                && !schedulePayload.ChangesSchedulePlan
                && !schedulePayload.ChangesScheduleActual))
        {
            throw new InvalidOperationException("Návrh neobsahuje žádnou změnu termínu ani harmonogramu.");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            // FIX 2026-05-01 (#5): supersede starého Pending PŘED inserted nového, aby
            // filtered unique index UX_zaznam_navrhy_pending_schedule_per_record nevybuchl.
            // V Serializable TX je atomické pořadí: UPDATE old.Stav='SUPERSEDED' → INSERT new.
            // Mezi tím není window kde by existovaly 2 Pending records.
            ProposalAuditSnapshot? oldSnapshotBefore = null;
            ZaznamNavrhEntity? oldProposal = null;
            if (supersededByPlaceholderProposalId is int oldId)
            {
                oldProposal = await _dbContext.ZaznamNavrhy.FirstAsync(n => n.Id == oldId, ct);
                oldSnapshotBefore = ProposalAuditSnapshot.FromEntity(oldProposal);
                oldProposal.Stav = RecordProposalStateCodes.Superseded;
                // SupersededByProposalId zůstane null pro tento moment, doplní se po insertu nového
                await _dbContext.SaveChangesAsync(ct);
            }

            var entity = new ZaznamNavrhEntity
            {
                ProjektId = command.ProjektId,
                ZaznamId = record.Id,
                SubsystemId = record.SubsystemId,
                TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
                Stav = RecordProposalStateCodes.Pending,
                PayloadJson = JsonSerializer.Serialize(payload),
                CreatedByOsobaId = currentUser.OsobaId,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            _dbContext.ZaznamNavrhy.Add(entity);
            await _dbContext.SaveChangesAsync(ct);

            // Po vytvoření nového dohrát SupersededByProposalId pro oldProposal + audit.
            if (oldProposal is not null)
            {
                oldProposal.SupersededByProposalId = entity.Id;
                await _dbContext.SaveChangesAsync(ct);
                _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.RecordProposal,
                    oldProposal.Id.ToString(CultureInfo.InvariantCulture),
                    oldSnapshotBefore,
                    ProposalAuditSnapshot.FromEntity(oldProposal)));
            }

            _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.RecordProposal,
                entity.Id.ToString(CultureInfo.InvariantCulture),
                null,
                ProposalAuditSnapshot.FromEntity(entity)));
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    // --- Private helpers used only by SubmitCommands ---

    private void ValidateCommonProposalInput(SaveRecordCommand command)
    {
        if (command.TerminUkonceni.Date < command.DatumZalozeni.Date)
        {
            throw new InvalidOperationException("Termín ukončení nesmí být dříve než datum založení.");
        }

        if (!command.VlastnikId.HasValue || command.VlastnikId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte vlastníka z nabídky osob.");
        }

        if (string.IsNullOrWhiteSpace(command.Nazev))
        {
            throw new InvalidOperationException("Název záznamu je povinný.");
        }

        if (string.IsNullOrWhiteSpace(command.Kategorie))
        {
            throw new InvalidOperationException("Kategorie záznamu je povinná.");
        }

        if (string.IsNullOrWhiteSpace(command.Stav))
        {
            throw new InvalidOperationException("Stav úkolu je povinný.");
        }

        ValidateManualActualKroky(command.ManualActualKroky);
        ValidateHarmonogramVazby(command.HarmonogramVazby, command.ExterniVazby.Count);
    }

    private void ValidateScheduleProposalInput(SaveRecordCommand command, ProjektovyZaznamEntity record)
    {
        if (command.TerminUkonceni.Date < record.DatumZalozeni.Date)
        {
            throw new InvalidOperationException("Termín ukončení nesmí být dříve než datum založení záznamu.");
        }

        ValidateManualActualKroky(command.ManualActualKroky);
    }

    private void ValidateManualActualKroky(IReadOnlyList<ManualActualKrokDto> manualKroky)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime.Date);
        ManualProposalFieldValidator.ValidateManualActualKroky(manualKroky, today);
    }

    private static void ValidateHarmonogramVazby(IReadOnlyList<HarmonogramVazbaDto> vazby, int externiVazbyCount)
        => ManualProposalFieldValidator.ValidateHarmonogramVazby(vazby, externiVazbyCount);

    private async Task EnsureCreateProposalMeetingSelectionAsync(SaveRecordCommand command, CancellationToken ct)
    {
        var projectUsesMeetingNumbering = await _dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == command.ProjektId)
            .Select(x => (bool?)x.PouzivatIdentJednani)
            .FirstOrDefaultAsync(ct);
        if (projectUsesMeetingNumbering != true)
        {
            return;
        }

        if (!command.JednaniIdProCislo.HasValue || command.JednaniIdProCislo.Value <= 0)
        {
            throw new InvalidOperationException("Pro tento projekt musíte vybrat jednání pro identifikátor.");
        }

        var meeting = await (
                from item in _dbContext.Jednani.AsNoTracking()
                join status in _dbContext.CiselnikStavuJednani.AsNoTracking() on item.StavJednaniId equals status.Id
                where item.Id == command.JednaniIdProCislo.Value
                    && item.ProjektId == command.ProjektId
                select new
                {
                    item.Id,
                    StavKod = status.Kod,
                    item.UzamklOsobaId
                })
            .FirstOrDefaultAsync(ct);

        if (meeting is null
            || string.Equals(meeting.StavKod, "CLOSED", StringComparison.OrdinalIgnoreCase)
            || meeting.UzamklOsobaId.HasValue)
        {
            throw new InvalidOperationException("Vybrané jednání pro identifikátor neexistuje nebo je uzavřené.");
        }
    }

    private async Task<int?> ResolveProjectSubsystemIdAsync(int projectId, string subsystemValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subsystemValue))
        {
            return null;
        }

        var normalized = subsystemValue.Trim();
        return await (
                from mapping in _dbContext.ProjektSubsystemy.AsNoTracking()
                join subsystem in _dbContext.Subsystemy.AsNoTracking() on mapping.SubsystemId equals subsystem.Id
                where mapping.ProjektId == projectId
                    && !mapping.DatumOdebrani.HasValue
                    && (subsystem.Kod == normalized || subsystem.Nazev == normalized)
                select (int?)subsystem.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Dictionary<int, int>> LoadExistingScheduleValuesAsync(
        int recordId,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions,
        CancellationToken ct)
    {
        var allowedTypeIds = scheduleTypeDefinitions
            .SelectMany(definition => new[] { definition.DurationTypeId, definition.DelayTypeId })
            .Where(typeId => typeId > 0)
            .Distinct()
            .ToList();
        if (allowedTypeIds.Count == 0)
        {
            return [];
        }

        // DESIGN-10-A (2026-05-01): NULL HodnotaInt = "krok nenastal" → vyfiltrovat z dictionary,
        // klíč chybí = caller (TimelineCalculator) interpretuje jako missing → offset = 0.
        return (await _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == recordId && allowedTypeIds.Contains(x.TypId) && x.HodnotaInt.HasValue)
                .Select(x => new { x.TypId, Hodnota = x.HodnotaInt!.Value })
                .ToListAsync(ct))
            .ToDictionary(x => x.TypId, x => x.Hodnota);
    }

    /// <summary>
    /// Phase 6 (DESIGN-5-A + 7-A, 2026-05-01) — odmítne návrh obsahující DELAY hodnoty pro
    /// auto-fillované kroky (= NENÍ v <see cref="HarmonogramManualSteps.KrokPoradi"/>).
    ///
    /// Konzervativní invariant: krok je auto-fillovatelný v některém typu PMP/PNF, takže
    /// jeho DELAY nemůže být v návrhu. Manuální 2/5/8/9 jsou bezpečné.
    /// </summary>
    private async Task ValidateAutoStepsNotInScheduleProposalAsync(
        SaveRecordCommand command,
        ProjektovyZaznamEntity record,
        CancellationToken ct)
    {
        if (command.HarmonogramHodnoty.Count == 0) return;

        // Schema: TypId → KrokPoradi map pro DELAY řádky aktivního schématu.
        var delayPoradiByTypId = await _dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(t => t.SablonaVerze == record.HarmonogramSablonaVerze && t.JeZpozdeni)
            .Select(t => new { t.Id, t.KrokPoradi })
            .ToDictionaryAsync(x => x.Id, x => x.KrokPoradi, ct);

        // Auto-fillovatelné DelayTypIds = poradi NENÍ v {2, 5, 8, 9}.
        var autoFilledDelayTypIds = delayPoradiByTypId
            .Where(kv => !HarmonogramManualSteps.IsManual(kv.Value))
            .Select(kv => kv.Key)
            .ToHashSet();

        ManualProposalFieldValidator.ValidateAutoStepNotInProposal(
            command.HarmonogramHodnoty, autoFilledDelayTypIds);
    }
}
