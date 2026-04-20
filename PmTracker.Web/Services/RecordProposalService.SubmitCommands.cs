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
        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _dbContext.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            ProjektId = command.ProjektId,
            SubsystemId = subsystemId.Value,
            TypNavrhu = RecordProposalTypeCodes.CreateRecord,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedByOsobaId = currentUser.OsobaId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _dbContext.SaveChangesAsync(ct);
        var createdProposal = await _dbContext.ZaznamNavrhy
            .OrderByDescending(x => x.Id)
            .FirstAsync(x => x.ProjektId == command.ProjektId && x.CreatedByOsobaId == currentUser.OsobaId, ct);
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.RecordProposal,
            createdProposal.Id.ToString(CultureInfo.InvariantCulture),
            null,
            ProposalAuditSnapshot.FromEntity(createdProposal)));
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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

        var pendingLock = await _pendingScheduleProposalLockEvaluator.EvaluateAsync(record.Id, ct);
        if (pendingLock.HasPendingProposal)
        {
            throw new InvalidOperationException(pendingLock.Message ?? "Pro tento záznam už existuje čekající návrh změny harmonogramu.");
        }

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

        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _dbContext.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            ProjektId = command.ProjektId,
            ZaznamId = record.Id,
            SubsystemId = record.SubsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedByOsobaId = currentUser.OsobaId,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        });
        await _dbContext.SaveChangesAsync(ct);
        var createdProposal = await _dbContext.ZaznamNavrhy
            .OrderByDescending(x => x.Id)
            .FirstAsync(x => x.ZaznamId == record.Id && x.CreatedByOsobaId == currentUser.OsobaId, ct);
        _auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Create,
            AuditEntityType.RecordProposal,
            createdProposal.Id.ToString(CultureInfo.InvariantCulture),
            null,
            ProposalAuditSnapshot.FromEntity(createdProposal)));
        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // --- Private helpers used only by SubmitCommands ---

    private static void ValidateCommonProposalInput(SaveRecordCommand command)
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
    }

    private static void ValidateScheduleProposalInput(SaveRecordCommand command, ProjektovyZaznamEntity record)
    {
        if (command.TerminUkonceni.Date < record.DatumZalozeni.Date)
        {
            throw new InvalidOperationException("Termín ukončení nesmí být dříve než datum založení záznamu.");
        }
    }

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

        return (await _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == recordId && allowedTypeIds.Contains(x.TypId))
                .ToListAsync(ct))
            .ToDictionary(x => x.TypId, x => x.HodnotaInt);
    }
}
