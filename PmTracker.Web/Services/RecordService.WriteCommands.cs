using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

public sealed partial class RecordService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const byte RecordDisplayNumberTypeIncrement = 0;
    private const byte RecordDisplayNumberTypeMeeting = 1;

    private sealed record SaveRecordProjectContext(
        int Id,
        bool PouzivatIdentJednani);

    private sealed record SaveRecordMeetingContext(
        int Id,
        int CisloJednani);

    private sealed record OpenMeetingRow(
        int Id,
        int CisloJednani);

    private sealed record SaveRecordValidationContext(
        int OwnerId,
        int KategorieId,
        int StavUkoluId,
        int? TypUkoluId,
        int SubsystemId,
        int DefaultSchemaVersion,
        bool IsTaskCategory,
        SaveRecordProjectContext Project,
        ProjektovyZaznamEntity? ExistingRecord,
        SaveRecordMeetingContext? MeetingForNumbering,
        IReadOnlyList<int> NormalizedCollaborationIds);

    public async Task<int> SaveRecordAsync(
        SaveRecordCommand command,
        CurrentUserContextViewModel currentUser,
        IRecordWriteCommandsComposition composition,
        CancellationToken ct = default)
    {
        var canEditRecord = currentUser.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId);
        var canEditScheduleFull = currentUser.HasPermission(PermissionKeys.RecordsScheduleEdit, command.ProjektId);
        var canAddSchedule = currentUser.HasPermission(PermissionKeys.RecordsScheduleAdd, command.ProjektId);
        if (!canEditRecord)
        {
            if (!canEditScheduleFull && !canAddSchedule)
            {
                throw new InvalidOperationException("Nemáte oprávnění upravovat tento záznam.");
            }

            return await SaveRecordScheduleOnlyAsync(command, currentUser, canEditScheduleFull, composition, ct);
        }

        var validation = await ValidateRecordSaveCommandAsync(command, composition, ct);
        var ownerId = validation.OwnerId;
        var categoryId = validation.KategorieId;
        var statusId = validation.StavUkoluId;
        var typeId = validation.TypUkoluId;
        var subsystemId = validation.SubsystemId;
        var defaultSchemaVersion = validation.DefaultSchemaVersion;
        var isTaskCategory = validation.IsTaskCategory;
        var project = validation.Project;
        var normalizedCollaborationIds = validation.NormalizedCollaborationIds;
        var pendingScheduleProposalLock = validation.ExistingRecord is not null
            ? await pendingScheduleProposalLockEvaluator.EvaluateAsync(validation.ExistingRecord.Id, ct)
            : new PendingScheduleProposalLockState(false, null, null, false, false);
        var oldRecordSnapshot = validation.ExistingRecord is null
            ? null
            : RecordAuditSnapshot.FromEntity(validation.ExistingRecord);

        var (tx, ownsTransaction) = await BeginSerializableTransactionIfNeededAsync(ct);
        await using var _ = tx;

        ProjektovyZaznamEntity entity;
        if (command.Id.HasValue)
        {
            entity = await dbContext.ProjektoveZaznamy
                .FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Záznam {command.Id.Value} nebyl nalezen.");

            var oldOwner = entity.VlastnikId;
            var oldDate = entity.DatumUkonceni;
            var oldSubsystem = entity.SubsystemId;
            var oldType = entity.AktualniTypUkoluId;
            var oldStatus = entity.StavUkoluId;

            entity.KategorieId = categoryId;
            entity.StavUkoluId = statusId;
            entity.AktualniTypUkoluId = typeId;
            entity.Nazev = command.Nazev.Trim();
            entity.Cil = string.IsNullOrWhiteSpace(command.Cil) ? null : command.Cil.Trim();
            var normalizedDescription = richTextContentService.NormalizeForStorage(command.Popis?.Trim());
            entity.Popis = string.IsNullOrWhiteSpace(normalizedDescription) ? null : normalizedDescription;
            entity.VlastnikId = ownerId;
            entity.DatumZalozeni = command.DatumZalozeni.Date;
            entity.DatumUkonceni = pendingScheduleProposalLock.LocksTermDeadline
                ? entity.DatumUkonceni.Date
                : command.TerminUkonceni.Date;
            entity.SubsystemId = subsystemId;
            if (string.IsNullOrWhiteSpace(entity.CisloViditelne))
            {
                entity.CisloViditelne = entity.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
                entity.CisloViditelneTyp = RecordDisplayNumberTypeIncrement;
                entity.CisloViditelneA = entity.CisloZaznamu;
                entity.CisloViditelneB = 0;
                entity.CisloJednaniZdrojId = null;
            }

            if (entity.HarmonogramSablonaVerze <= 0)
            {
                entity.HarmonogramSablonaVerze = defaultSchemaVersion;
            }

            await dbContext.SaveChangesAsync(ct);

            if (oldOwner != entity.VlastnikId)
            {
                dbContext.ZaznamHistorieVlastnik.Add(new ZaznamHistorieVlastnikEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniVlastnik = oldOwner,
                    NovyVlastnik = entity.VlastnikId,
                    DatumZmeny = GetLocalNow()
                });
            }

            if (oldDate.Date != entity.DatumUkonceni.Date)
            {
                dbContext.ZaznamHistorieTerminu.Add(new ZaznamHistorieTerminuEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniDatum = oldDate.Date,
                    NoveDatum = entity.DatumUkonceni.Date,
                    DatumZmeny = GetLocalNow(),
                    Duvod = "Úprava záznamu"
                });
            }

            if (oldSubsystem != entity.SubsystemId)
            {
                dbContext.ZaznamHistorieSubsystem.Add(new ZaznamHistorieSubsystemEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniSubsystem = oldSubsystem,
                    NovySubsystem = entity.SubsystemId,
                    DatumZmeny = GetLocalNow()
                });
            }

            if (oldType != entity.AktualniTypUkoluId && oldType.HasValue && entity.AktualniTypUkoluId.HasValue)
            {
                dbContext.ZaznamHistorieZmenTypu.Add(new ZaznamHistorieZmenTypuEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniTypId = oldType.Value,
                    NovyTypId = entity.AktualniTypUkoluId.Value,
                    DatumZmeny = GetLocalNow(),
                    ZmenilOsobaId = currentUser.OsobaId
                });
            }

            if (oldStatus != entity.StavUkoluId && oldStatus.HasValue && entity.StavUkoluId.HasValue)
            {
                dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
                {
                    ZaznamId = entity.Id,
                    PuvodniStav = oldStatus.Value,
                    NovyStav = entity.StavUkoluId.Value,
                    DatumZmeny = DateTime.UtcNow
                });
            }
        }
        else
        {
            var requestedCislo = command.CisloZaznamu > 0 ? command.CisloZaznamu : 0;
            var cislo = requestedCislo;
            if (cislo <= 0
                || await dbContext.ProjektoveZaznamy.AnyAsync(x => x.ProjektId == command.ProjektId && x.CisloZaznamu == cislo, ct))
            {
                cislo = await composition.GetNextCisloZaznamuTransactionalAsync(command.ProjektId, ct);
            }

            var cisloViditelneTyp = RecordDisplayNumberTypeIncrement;
            var cisloViditelneA = cislo;
            var cisloViditelneB = 0;
            int? cisloJednaniZdrojId = null;
            var cisloViditelne = cislo.ToString(CultureInfo.InvariantCulture);

            if (project.PouzivatIdentJednani)
            {
                var meeting = validation.MeetingForNumbering
                    ?? throw new InvalidOperationException("Vybrané jednání pro identifikátor nebylo validováno.");
                var nextOrder = await composition.AllocateMeetingOrderTransactionalAsync(command.ProjektId, meeting.CisloJednani, ct);
                cisloViditelneTyp = RecordDisplayNumberTypeMeeting;
                cisloViditelneA = meeting.CisloJednani;
                cisloViditelneB = nextOrder;
                cisloJednaniZdrojId = meeting.Id;
                cisloViditelne = $"{meeting.CisloJednani}-{nextOrder}";
            }

            var normalizedDescription = richTextContentService.NormalizeForStorage(command.Popis?.Trim());
            entity = new ProjektovyZaznamEntity
            {
                ProjektId = command.ProjektId,
                KategorieId = categoryId,
                StavUkoluId = statusId,
                AktualniTypUkoluId = typeId,
                CisloZaznamu = cislo,
                CisloViditelne = cisloViditelne,
                CisloViditelneTyp = cisloViditelneTyp,
                CisloViditelneA = cisloViditelneA,
                CisloViditelneB = cisloViditelneB,
                CisloJednaniZdrojId = cisloJednaniZdrojId,
                Nazev = command.Nazev.Trim(),
                Cil = string.IsNullOrWhiteSpace(command.Cil) ? null : command.Cil.Trim(),
                Popis = string.IsNullOrWhiteSpace(normalizedDescription) ? null : normalizedDescription,
                VlastnikId = ownerId,
                DatumZalozeni = command.DatumZalozeni.Date,
                DatumUkonceni = command.TerminUkonceni.Date,
                SubsystemId = subsystemId,
                HarmonogramSablonaVerze = defaultSchemaVersion
            };
            dbContext.ProjektoveZaznamy.Add(entity);
        }

        await dbContext.SaveChangesAsync(ct);
        await ReplaceRecordCollaborationAsync(entity.Id, normalizedCollaborationIds, ct);
        await ReplaceRecordExternalLinksAsync(entity.Id, command.ExterniVazby, ct);

        List<SaveRecordHarmonogramValueCommand>? normalizedScheduleValues = null;
        RecordScheduleAuditSnapshot? oldScheduleSnapshot = null;
        if (command.Id.HasValue)
        {
            oldScheduleSnapshot = await LoadRecordScheduleAuditSnapshotAsync(command.Id.Value, ct);
        }

        if (isTaskCategory && command.HarmonogramHodnoty.Count > 0)
        {
            var scheduleTypeDefinitions = await composition.ResolveScheduleTypeDefinitionsForRecordAsync(entity, ct);
            var valuesToPersist = pendingScheduleProposalLock.LocksSchedule
                ? await BuildScheduleValuesPreservingLockedScopeAsync(entity.Id, command.HarmonogramHodnoty, scheduleTypeDefinitions, pendingScheduleProposalLock, ct)
                : command.HarmonogramHodnoty;
            normalizedScheduleValues = await ReplaceRecordScheduleValuesAsync(entity.Id, valuesToPersist, scheduleTypeDefinitions, ct);
        }
        else if (!isTaskCategory)
        {
            var existingScheduleValues = await dbContext.ZaznamHarmonogramHodnoty
                .Where(x => x.ZaznamId == entity.Id)
                .ToListAsync(ct);
            if (existingScheduleValues.Count > 0)
            {
                dbContext.ZaznamHarmonogramHodnoty.RemoveRange(existingScheduleValues);
                normalizedScheduleValues = [];
            }
        }

        await dbContext.SaveChangesAsync(ct);
        await priorityMatrixRebuildService.RebuildForRecordAsync(entity.Id, ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            command.Id.HasValue ? AuditActionType.Update : AuditActionType.Create,
            AuditEntityType.Record,
            entity.Id.ToString(CultureInfo.InvariantCulture),
            oldRecordSnapshot,
            RecordAuditSnapshot.FromEntity(entity)));
        if (normalizedScheduleValues is not null)
        {
            var newScheduleSnapshot = await LoadRecordScheduleAuditSnapshotAsync(entity.Id, ct);
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                oldScheduleSnapshot is null ? AuditActionType.Create : AuditActionType.Update,
                AuditEntityType.RecordSchedule,
                entity.Id.ToString(CultureInfo.InvariantCulture),
                oldScheduleSnapshot,
                newScheduleSnapshot));
        }

        await dbContext.SaveChangesAsync(ct);
        if (ownsTransaction && tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return entity.Id;
    }

    public async Task DeleteRecordAsync(DeleteRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var record = await dbContext.ProjektoveZaznamy
            .FirstOrDefaultAsync(x => x.Id == command.ZaznamId, ct)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");

        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var historyTypeRows = await dbContext.ZaznamHistorieZmenTypu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyDeadlineRows = await dbContext.ZaznamHistorieTerminu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyOwnerRows = await dbContext.ZaznamHistorieVlastnik.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historySubsystemRows = await dbContext.ZaznamHistorieSubsystem.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyStateRows = await dbContext.ZaznamHistorieStavuZaznamu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var historyProjectStateRows = await dbContext.ZaznamHistorieStavuProjektu.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var externalLinkRows = await dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var collaborationRows = await dbContext.ZaznamSpoluprace.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var scheduleRows = await dbContext.ZaznamHarmonogramHodnoty.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var commentRows = await dbContext.Vyjadreni.Where(x => x.ZaznamId == command.ZaznamId).ToListAsync(ct);
        var oldRecord = RecordAuditSnapshot.FromEntity(record);
        var oldScheduleSnapshot = RecordScheduleAuditSnapshot.FromEntities(command.ZaznamId, scheduleRows);
        var oldCommentSnapshots = commentRows.Select(CommentAuditSnapshot.FromEntity).ToList();

        if (historyTypeRows.Count > 0)
        {
            dbContext.ZaznamHistorieZmenTypu.RemoveRange(historyTypeRows);
        }

        if (historyDeadlineRows.Count > 0)
        {
            dbContext.ZaznamHistorieTerminu.RemoveRange(historyDeadlineRows);
        }

        if (historyOwnerRows.Count > 0)
        {
            dbContext.ZaznamHistorieVlastnik.RemoveRange(historyOwnerRows);
        }

        if (historySubsystemRows.Count > 0)
        {
            dbContext.ZaznamHistorieSubsystem.RemoveRange(historySubsystemRows);
        }

        if (historyStateRows.Count > 0)
        {
            dbContext.ZaznamHistorieStavuZaznamu.RemoveRange(historyStateRows);
        }

        if (historyProjectStateRows.Count > 0)
        {
            dbContext.ZaznamHistorieStavuProjektu.RemoveRange(historyProjectStateRows);
        }

        if (externalLinkRows.Count > 0)
        {
            dbContext.ZaznamExterniOdkazy.RemoveRange(externalLinkRows);
        }

        if (collaborationRows.Count > 0)
        {
            dbContext.ZaznamSpoluprace.RemoveRange(collaborationRows);
        }

        if (scheduleRows.Count > 0)
        {
            dbContext.ZaznamHarmonogramHodnoty.RemoveRange(scheduleRows);
        }

        if (commentRows.Count > 0)
        {
            dbContext.Vyjadreni.RemoveRange(commentRows);
        }

        var priorityRows = await dbContext.ZaznamPriorityUzivatelu
            .Where(x => x.ZaznamId == command.ZaznamId)
            .ToListAsync(ct);
        if (priorityRows.Count > 0)
        {
            dbContext.ZaznamPriorityUzivatelu.RemoveRange(priorityRows);
        }

        if (historyTypeRows.Count > 0
            || historyDeadlineRows.Count > 0
            || historyOwnerRows.Count > 0
            || historySubsystemRows.Count > 0
            || historyStateRows.Count > 0
            || historyProjectStateRows.Count > 0
            || externalLinkRows.Count > 0
            || collaborationRows.Count > 0
            || scheduleRows.Count > 0
            || commentRows.Count > 0
            || priorityRows.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        dbContext.ProjektoveZaznamy.Remove(record);
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Delete,
            AuditEntityType.Record,
            command.ZaznamId.ToString(CultureInfo.InvariantCulture),
            oldRecord,
            null));
        if (scheduleRows.Count > 0)
        {
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Delete,
                AuditEntityType.RecordSchedule,
                command.ZaznamId.ToString(CultureInfo.InvariantCulture),
                oldScheduleSnapshot,
                null));
        }

        foreach (var oldCommentSnapshot in oldCommentSnapshots)
        {
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Delete,
                AuditEntityType.Comment,
                oldCommentSnapshot.Id.ToString(CultureInfo.InvariantCulture),
                oldCommentSnapshot,
                null));
        }

        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task AssignMeetingIdentifierAsync(
        AssignMeetingIdentifierCommand command,
        CurrentUserContextViewModel currentUser,
        IRecordWriteCommandsComposition composition,
        CancellationToken ct = default)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var record = await dbContext.ProjektoveZaznamy
            .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE id = {0}", command.ZaznamId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");
        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            throw new InvalidOperationException("Záznam už má identifikátor podle jednání.");
        }

        var openMeetings = await LoadOpenProjectMeetingsAsync(command.ProjektId, ct);
        if (openMeetings.Count == 0)
        {
            throw new InvalidOperationException("Není dostupné žádné neuzavřené jednání.");
        }

        var meeting = openMeetings
            .FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException("Vybrané jednání neexistuje.");

        var nextOrder = await composition.AllocateMeetingOrderTransactionalAsync(command.ProjektId, meeting.CisloJednani, ct);
        var old = RecordAuditSnapshot.FromEntity(record);
        record.CisloViditelneTyp = RecordDisplayNumberTypeMeeting;
        record.CisloViditelneA = meeting.CisloJednani;
        record.CisloViditelneB = nextOrder;
        record.CisloJednaniZdrojId = meeting.Id;
        record.CisloViditelne = $"{meeting.CisloJednani}-{nextOrder}";
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Assign,
            AuditEntityType.Record,
            record.Id.ToString(CultureInfo.InvariantCulture),
            old,
            RecordAuditSnapshot.FromEntity(record)));
        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<SaveRecordValidationContext> ValidateRecordSaveCommandAsync(
        SaveRecordCommand command,
        IRecordWriteCommandsComposition composition,
        CancellationToken ct)
    {
        var issues = new List<RecordValidationIssue>();

        ValidateAllowedControlCharacters(command.Nazev, "Nazev", "basic", "Název", issues);
        ValidateAllowedControlCharacters(command.Cil, "Cil", "basic", "Cíl", issues);
        ValidateAllowedControlCharacters(command.Popis, "Popis", "basic", "Popis", issues);

        if (command.TerminUkonceni.Date < command.DatumZalozeni.Date)
        {
            AddRecordValidationIssue(
                issues,
                "TerminUkonceni",
                "Termín ukončení nesmí být dříve než datum založení.",
                "basic",
                "termin_not_before_start",
                command.TerminUkonceni.ToString("O", CultureInfo.InvariantCulture));
        }

        if (!command.VlastnikId.HasValue || command.VlastnikId.Value <= 0)
        {
            AddRecordValidationIssue(
                issues,
                "VlastnikId",
                "Vyberte vlastníka z nabídky osob.",
                "basic",
                "owner_required",
                command.VlastnikId?.ToString(CultureInfo.InvariantCulture));
        }

        var ownerId = command.VlastnikId.GetValueOrDefault();
        var categoryId = await TryResolveForValidationAsync(
            issues,
            "Kategorie",
            "basic",
            "category_not_found",
            command.Kategorie,
            () => ResolveKategorieIdAsync(command.Kategorie, ct));
        var statusId = await TryResolveForValidationAsync(
            issues,
            "Stav",
            "basic",
            "task_status_not_found",
            command.Stav,
            () => ResolveStavUkoluIdAsync(command.Stav, ct));
        var subsystemId = await TryResolveForValidationAsync(
            issues,
            "Subsystem",
            "basic",
            "subsystem_invalid_or_inactive",
            command.Subsystem,
            () => ResolveProjectSubsystemIdAsync(command.ProjektId, command.Subsystem, ct));

        int? typeId = null;
        if (!string.IsNullOrWhiteSpace(command.TypUkolu))
        {
            typeId = await ResolveTypUkoluIdAsync(command.TypUkolu, ct);
            if (!typeId.HasValue)
            {
                AddRecordValidationIssue(
                    issues,
                    "TypUkolu",
                    $"Typ úkolu '{command.TypUkolu}' neexistuje.",
                    "basic",
                    "task_type_not_found",
                    command.TypUkolu);
            }
        }

        var category = categoryId.HasValue
            ? await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
                .Where(x => x.Id == categoryId.Value)
                .Select(x => new { x.Kod, x.Nazev })
                .FirstOrDefaultAsync(ct)
            : null;
        var isTaskCategory = IsTaskCategory(category?.Kod, category?.Nazev);
        var defaultSchemaVersion = await composition.EnsurePersistedActiveHarmonogramSchemaVersionAsync(ct);

        var project = await dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == command.ProjektId)
            .Select(x => new SaveRecordProjectContext(x.Id, x.PouzivatIdentJednani))
            .FirstOrDefaultAsync(ct);
        if (project is null)
        {
            AddRecordValidationIssue(
                issues,
                "ProjektId",
                $"Projekt {command.ProjektId} nebyl nalezen.",
                "basic",
                "project_not_found",
                command.ProjektId.ToString(CultureInfo.InvariantCulture));
        }

        var existingRecord = command.Id.HasValue
            ? await dbContext.ProjektoveZaznamy.AsNoTracking().FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
            : null;
        if (command.Id.HasValue && existingRecord is null)
        {
            AddRecordValidationIssue(
                issues,
                "Id",
                $"Záznam {command.Id.Value} nebyl nalezen.",
                "basic",
                "record_not_found",
                command.Id.Value.ToString(CultureInfo.InvariantCulture));
        }
        else if (existingRecord is not null && existingRecord.ProjektId != command.ProjektId)
        {
            AddRecordValidationIssue(
                issues,
                "Id",
                "Záznam nepatří do vybraného projektu.",
                "basic",
                "record_project_mismatch",
                command.Id?.ToString(CultureInfo.InvariantCulture));
        }

        SaveRecordMeetingContext? meetingForNumbering = null;
        if (project?.PouzivatIdentJednani == true && !command.Id.HasValue)
        {
            var openMeetings = await LoadOpenProjectMeetingsAsync(command.ProjektId, ct);

            if (openMeetings.Count == 0)
            {
                AddRecordValidationIssue(
                    issues,
                    "JednaniIdProCislo",
                    "Není dostupné žádné neuzavřené jednání.",
                    "basic",
                    "open_meeting_required",
                    null);
            }
            else if (!command.JednaniIdProCislo.HasValue || command.JednaniIdProCislo.Value <= 0)
            {
                AddRecordValidationIssue(
                    issues,
                    "JednaniIdProCislo",
                    "Pro tento režim vyberte jednání.",
                    "basic",
                    "meeting_selection_required",
                    command.JednaniIdProCislo?.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                var selectedMeeting = openMeetings.FirstOrDefault(x => x.Id == command.JednaniIdProCislo.Value);
                if (selectedMeeting is null)
                {
                    AddRecordValidationIssue(
                        issues,
                        "JednaniIdProCislo",
                        "Vybrané jednání neexistuje nebo je uzavřené. Vyberte neuzavřené jednání.",
                        "basic",
                        "meeting_closed_or_missing",
                        command.JednaniIdProCislo.Value.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    meetingForNumbering = new SaveRecordMeetingContext(selectedMeeting.Id, selectedMeeting.CisloJednani);
                }
            }
        }

        var normalizedCollaborationIds = command.VybraniSpolupracovniciIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (project is not null)
        {
            var allowedOwnerIds = (await composition.BuildRecordOwnerCandidatesAsync(command.ProjektId, existingRecord?.VlastnikId, ct))
                .Select(x => x.OsobaId)
                .ToHashSet();
            if (ownerId > 0 && !allowedOwnerIds.Contains(ownerId))
            {
                AddRecordValidationIssue(
                    issues,
                    "VlastnikId",
                    "Vyberte vlastníka z nabídky osob projektu.",
                    "basic",
                    "owner_not_allowed",
                    ownerId.ToString(CultureInfo.InvariantCulture));
            }

            var allowedCollaborationIds = (await composition.BuildRecordOwnerCandidatesAsync(command.ProjektId, null, ct))
                .Select(x => x.OsobaId)
                .ToHashSet();
            var invalidCollaborationIds = normalizedCollaborationIds
                .Where(x => !allowedCollaborationIds.Contains(x))
                .Distinct()
                .ToList();
            if (invalidCollaborationIds.Count > 0)
            {
                AddRecordValidationIssue(
                    issues,
                    "VybraniSpolupracovniciIds",
                    "Spolupracovníci musí být vybráni z osob projektu.",
                    "collaboration",
                    "collaborator_not_allowed",
                    string.Join(", ", invalidCollaborationIds));
            }
        }

        await ValidateExternalLinksAsync(command.ExterniVazby, issues, ct);
        await ValidateScheduleValuesAsync(command, isTaskCategory, existingRecord, defaultSchemaVersion, composition, issues, ct);

        if (issues.Count > 0)
        {
            throw new RecordValidationException(
                "Záznam nelze uložit. Opravte označená pole v jednotlivých záložkách.",
                issues,
                BuildRecordValidationDiagnosticLog(command, issues));
        }

        return new SaveRecordValidationContext(
            ownerId,
            categoryId!.Value,
            statusId!.Value,
            typeId,
            subsystemId!.Value,
            defaultSchemaVersion,
            isTaskCategory,
            project!,
            existingRecord,
            meetingForNumbering,
            normalizedCollaborationIds);
    }

    private static async Task<int?> TryResolveForValidationAsync(
        List<RecordValidationIssue> issues,
        string fieldKey,
        string tab,
        string rule,
        string? value,
        Func<Task<int>> resolver)
    {
        try
        {
            return await resolver();
        }
        catch (InvalidOperationException ex)
        {
            AddRecordValidationIssue(issues, fieldKey, ex.Message, tab, rule, value);
            return null;
        }
    }

    private static void ValidateAllowedControlCharacters(
        string? value,
        string fieldKey,
        string tab,
        string fieldLabel,
        List<RecordValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var invalid = FindFirstInvalidControlCharacter(value);
        if (!invalid.HasValue)
        {
            return;
        }

        AddRecordValidationIssue(
            issues,
            fieldKey,
            $"{fieldLabel} obsahuje nepovolený řídicí znak na pozici {invalid.Value.Index + 1} ({FormatUnicodeCodePoint(invalid.Value.CodePoint)}). Povolené jsou pouze CR, LF a TAB.",
            tab,
            "invalid_control_character",
            value);
    }

    private async Task ValidateExternalLinksAsync(
        IReadOnlyList<SaveRecordExterniVazbaCommand> links,
        List<RecordValidationIssue> issues,
        CancellationToken ct)
    {
        if (links.Count == 0)
        {
            return;
        }

        var typeRows = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync(ct);
        var vyzvaRows = await dbContext.CiselnikVyzvy.AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync(ct);

        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];
            var typeValue = (link.Typ ?? string.Empty).Trim();
            var cisloValue = (link.Cislo ?? string.Empty).Trim();
            var priceValue = (link.PredpokladanaCena ?? string.Empty).Trim();
            var vyzvaValue = (link.Vyzva ?? string.Empty).Trim();
            var rowPrefix = $"ExterniVazby[{index}]";

            var hasType = !string.IsNullOrWhiteSpace(typeValue);
            var hasCislo = !string.IsNullOrWhiteSpace(cisloValue);
            if (!hasType && !hasCislo && string.IsNullOrWhiteSpace(priceValue) && string.IsNullOrWhiteSpace(vyzvaValue)
                && !link.DatumObjednani.HasValue && !link.PlanDodani.HasValue && !link.DatumDodani.HasValue && !link.DatumPrevzeti.HasValue)
            {
                continue;
            }

            if (!hasType)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.Typ",
                    "Vyplňte typ odkazu.",
                    "external",
                    "external_type_required",
                    typeValue);
            }

            if (!hasCislo)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.Cislo",
                    "Vyplňte číslo externí vazby.",
                    "external",
                    "external_number_required",
                    cisloValue);
            }

            var selectedType = hasType
                ? typeRows.FirstOrDefault(x => string.Equals(x.Kod, typeValue, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Nazev, typeValue, StringComparison.OrdinalIgnoreCase))
                : null;
            if (hasType && selectedType is null)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.Typ",
                    $"Typ externí vazby '{typeValue}' neexistuje.",
                    "external",
                    "external_type_not_found",
                    typeValue);
            }

            if (!string.IsNullOrWhiteSpace(vyzvaValue))
            {
                var vyzvaExists = vyzvaRows.Any(x => string.Equals(x.Kod, vyzvaValue, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Nazev, vyzvaValue, StringComparison.OrdinalIgnoreCase));
                if (!vyzvaExists)
                {
                    AddRecordValidationIssue(
                        issues,
                        $"{rowPrefix}.Vyzva",
                        $"Výzva '{vyzvaValue}' neexistuje.",
                        "external",
                        "external_vyzva_not_found",
                        vyzvaValue);
                }
            }

            if (!string.IsNullOrWhiteSpace(priceValue))
            {
                try
                {
                    NormalizeEstimatedExternalLinkPrice(selectedType?.Kod ?? typeValue, priceValue);
                }
                catch (InvalidOperationException ex)
                {
                    AddRecordValidationIssue(
                        issues,
                        $"{rowPrefix}.PredpokladanaCena",
                        ex.Message,
                        "external",
                        "external_price_invalid",
                        priceValue);
                }
            }

            if (link.PlanDodani.HasValue && link.DatumObjednani.HasValue && link.PlanDodani.Value.Date < link.DatumObjednani.Value.Date)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.PlanDodani",
                    "Plán dodání nesmí být dříve než datum objednání.",
                    "external",
                    "external_plan_before_order",
                    link.PlanDodani.Value.ToString("O", CultureInfo.InvariantCulture));
            }

            if (link.DatumDodani.HasValue && link.DatumObjednani.HasValue && link.DatumDodani.Value.Date < link.DatumObjednani.Value.Date)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.DatumDodani",
                    "Datum dodání nesmí být dříve než datum objednání.",
                    "external",
                    "external_delivery_before_order",
                    link.DatumDodani.Value.ToString("O", CultureInfo.InvariantCulture));
            }

            if (link.DatumPrevzeti.HasValue && link.DatumDodani.HasValue && link.DatumPrevzeti.Value.Date < link.DatumDodani.Value.Date)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.DatumPrevzeti",
                    "Datum převzetí nesmí být dříve než datum dodání.",
                    "external",
                    "external_takeover_before_delivery",
                    link.DatumPrevzeti.Value.ToString("O", CultureInfo.InvariantCulture));
            }
        }
    }

    private async Task ValidateScheduleValuesAsync(
        SaveRecordCommand command,
        bool isTaskCategory,
        ProjektovyZaznamEntity? existingRecord,
        int defaultSchemaVersion,
        IRecordWriteCommandsComposition composition,
        List<RecordValidationIssue> issues,
        CancellationToken ct)
    {
        if (!isTaskCategory || command.HarmonogramHodnoty.Count == 0)
        {
            return;
        }

        IReadOnlyList<RecordScheduleTypeDefinition> typeDefinitions;
        try
        {
            typeDefinitions = existingRecord is not null && existingRecord.HarmonogramSablonaVerze > 0
                ? await composition.ResolveScheduleTypeDefinitionsForSchemaVersionAsync(existingRecord.HarmonogramSablonaVerze, ct)
                : await composition.ResolveScheduleTypeDefinitionsForSchemaVersionAsync(defaultSchemaVersion, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddRecordValidationIssue(
                issues,
                "HarmonogramHodnoty",
                ex.Message,
                "schedule",
                "schedule_schema_unavailable",
                null);
            return;
        }

        var durationTypeIds = typeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .ToHashSet();
        var allowedTypeIds = typeDefinitions
            .SelectMany(x => new[] { x.DurationTypeId, x.DelayTypeId })
            .Where(x => x > 0)
            .ToHashSet();

        var firstIndexByType = new Dictionary<int, int>();
        for (var index = 0; index < command.HarmonogramHodnoty.Count; index++)
        {
            var item = command.HarmonogramHodnoty[index];
            var rowPrefix = $"HarmonogramHodnoty[{index}]";

            if (item.TypId <= 0)
            {
                if (item.Hodnota != 0)
                {
                    AddRecordValidationIssue(
                        issues,
                        $"{rowPrefix}.TypId",
                        "Typ harmonogramové hodnoty musí být platný.",
                        "schedule",
                        "schedule_type_required",
                        item.TypId.ToString(CultureInfo.InvariantCulture));
                }

                continue;
            }

            if (!allowedTypeIds.Contains(item.TypId))
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.TypId",
                    $"Typ harmonogramové hodnoty {item.TypId} není v aktivním schématu.",
                    "schedule",
                    "schedule_type_not_allowed",
                    item.TypId.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (firstIndexByType.TryGetValue(item.TypId, out var firstIndex))
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.TypId",
                    $"Typ harmonogramové hodnoty {item.TypId} je zadaný vícekrát (první výskyt na řádku {firstIndex + 1}).",
                    "schedule",
                    "schedule_type_duplicate",
                    item.TypId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                firstIndexByType[item.TypId] = index;
            }

            if (durationTypeIds.Contains(item.TypId) && item.Hodnota < 0)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.Hodnota",
                    "Trvání kroku harmonogramu nesmí být záporné.",
                    "schedule",
                    "schedule_duration_negative",
                    item.Hodnota.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void AddRecordValidationIssue(
        ICollection<RecordValidationIssue> issues,
        string fieldKey,
        string message,
        string tab,
        string? rule,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        issues.Add(new RecordValidationIssue(fieldKey, message.Trim(), tab, rule, value));
    }

    private static (int Index, int CodePoint)? FindFirstInvalidControlCharacter(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsControl(current) && current != '\r' && current != '\n' && current != '\t')
            {
                return (index, char.ConvertToUtf32(value, index));
            }

            if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
            {
                index++;
            }
        }

        return null;
    }

    private static string FormatUnicodeCodePoint(int codePoint)
    {
        return codePoint <= 0xFFFF
            ? $"U+{codePoint:X4}"
            : $"U+{codePoint:X6}";
    }

    private static string BuildRecordValidationDiagnosticLog(
        SaveRecordCommand command,
        IReadOnlyList<RecordValidationIssue> issues)
    {
        var builder = new StringBuilder(4096);
        builder.AppendLine("Record save validation failed.");
        builder.AppendLine("Issues:");
        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index];
            builder.Append(index + 1)
                .Append(". [Tab: ")
                .Append(issue.Tab)
                .Append("] [Field: ")
                .Append(issue.FieldKey)
                .Append("] ")
                .Append(issue.Message);
            if (!string.IsNullOrWhiteSpace(issue.Rule))
            {
                builder.Append(" [Rule: ")
                    .Append(issue.Rule)
                    .Append(']');
            }

            if (issue.Value is not null)
            {
                builder.Append(" [Value: ")
                    .Append(issue.Value)
                    .Append(']');
            }

            builder.AppendLine();
        }

        builder.AppendLine("CommandValues:");
        builder.AppendLine(JsonSerializer.Serialize(command, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        return builder.ToString().TrimEnd();
    }

    private async Task<int> ResolveKategorieIdAsync(string value, CancellationToken ct)
        => await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Kategorie '{value}' neexistuje.");

    private static bool IsTaskCategory(string? categoryCode, string? categoryName)
        => RecordCategoryClassifier.IsTaskCategory(categoryCode, categoryName);

    private async Task<int> ResolveStavUkoluIdAsync(string value, CancellationToken ct)
        => await dbContext.CiselnikStavuUkolu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Stav úkolu '{value}' neexistuje.");

    private async Task<int> ResolveSubsystemIdAsync(string value, CancellationToken ct)
        => await dbContext.Subsystemy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Subsystém '{value}' neexistuje.");

    private async Task<int> ResolveProjectSubsystemIdAsync(int projektId, string value, CancellationToken ct)
    {
        var subsystemId = await ResolveSubsystemIdAsync(value, ct);
        var isActiveInProject = await dbContext.ProjektSubsystemy.AsNoTracking()
            .AnyAsync(x => x.ProjektId == projektId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue, ct);
        if (!isActiveInProject)
        {
            throw new InvalidOperationException("Vybraný subsystém není aktivně přiřazený projektu.");
        }

        return subsystemId;
    }

    private async Task<int> ResolveTypOdkazuIdAsync(string value, CancellationToken ct)
        => await dbContext.CiselnikTypuExternichOdkazu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Typ externí vazby '{value}' neexistuje.");

    private static bool SupportsEstimatedExternalLinkPrice(string? externalTypeCode)
        => !string.IsNullOrWhiteSpace(externalTypeCode)
            && (Ci.Equals(externalTypeCode, "PMP") || Ci.Equals(externalTypeCode, "PNF"));

    private static decimal? NormalizeEstimatedExternalLinkPrice(string? externalTypeCode, string? estimatedPrice)
    {
        if (!SupportsEstimatedExternalLinkPrice(externalTypeCode) || string.IsNullOrWhiteSpace(estimatedPrice))
        {
            return null;
        }

        var normalizedValue = estimatedPrice.Trim();
        if (!decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed)
            && !decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            throw new InvalidOperationException($"Předpokládaná cena externí vazby '{estimatedPrice}' není validní číslo.");
        }

        return decimal.Round(parsed, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<int?> ResolveTypUkoluIdAsync(string? value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return await dbContext.CiselnikTypuUkolu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<int?> ResolveVyzvaIdAsync(string? value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return await dbContext.CiselnikVyzvy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task ReplaceRecordCollaborationAsync(int zaznamId, IReadOnlyList<int> selectedPersonIds, CancellationToken ct)
    {
        var existing = await dbContext.ZaznamSpoluprace.Where(x => x.ZaznamId == zaznamId).ToListAsync(ct);
        dbContext.ZaznamSpoluprace.RemoveRange(existing);

        foreach (var personId in selectedPersonIds.Distinct())
        {
            dbContext.ZaznamSpoluprace.Add(new ZaznamSpolupraceEntity
            {
                ZaznamId = zaznamId,
                OsobaId = personId
            });
        }
    }

    private async Task ReplaceRecordExternalLinksAsync(int zaznamId, IReadOnlyList<SaveRecordExterniVazbaCommand> externalLinks, CancellationToken ct)
    {
        var existing = await dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == zaznamId).ToListAsync(ct);
        dbContext.ZaznamExterniOdkazy.RemoveRange(existing);

        foreach (var link in externalLinks)
        {
            if (string.IsNullOrWhiteSpace(link.Typ) || string.IsNullOrWhiteSpace(link.Cislo))
            {
                continue;
            }

            var typeId = await ResolveTypOdkazuIdAsync(link.Typ, ct);
            dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = zaznamId,
                TypOdkazuId = typeId,
                Cislo = link.Cislo.Trim(),
                PredpokladanaCena = NormalizeEstimatedExternalLinkPrice(link.Typ, link.PredpokladanaCena),
                DatumObjednani = link.DatumObjednani,
                PlanDodani = link.PlanDodani,
                DatumDodani = link.DatumDodani,
                DatumPrevzeti = link.DatumPrevzeti,
                Vyzva = await ResolveVyzvaIdAsync(link.Vyzva, ct)
            });
        }
    }

    private async Task<int> SaveRecordScheduleOnlyAsync(
        SaveRecordCommand command,
        CurrentUserContextViewModel currentUser,
        bool canEditScheduleFull,
        IRecordWriteCommandsComposition composition,
        CancellationToken ct)
    {
        if (!command.Id.HasValue || command.Id.Value <= 0)
        {
            throw new InvalidOperationException("Bez oprávnění records.edit nelze zakládat nový záznam.");
        }

        if (!string.Equals(command.EditorTab, "schedule", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bez oprávnění records.edit lze ukládat pouze záložku Harmonogram.");
        }

        var entity = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
            ?? throw new InvalidOperationException($"Záznam {command.Id.Value} nebyl nalezen.");
        if (entity.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do zvoleného projektu.");
        }

        var category = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == entity.KategorieId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefaultAsync(ct);
        if (!IsTaskCategory(category?.Kod, category?.Nazev))
        {
            throw new InvalidOperationException("Harmonogram lze upravovat pouze u záznamů kategorie úkol.");
        }

        await EnsureScheduleAddScopeAccessAsync(entity, currentUser, canEditScheduleFull, composition, ct);

        var scheduleTypeDefinitions = await composition.ResolveScheduleTypeDefinitionsForRecordAsync(entity, ct);
        var valuesToPersist = canEditScheduleFull
            ? command.HarmonogramHodnoty
            : await BuildScheduleValuesForAddOnlyAsync(entity.Id, command.HarmonogramHodnoty, scheduleTypeDefinitions, ct);

        var pendingScheduleProposalLock = await pendingScheduleProposalLockEvaluator.EvaluateAsync(entity.Id, ct);
        if (pendingScheduleProposalLock.LocksSchedule)
        {
            valuesToPersist = await BuildScheduleValuesPreservingLockedScopeAsync(entity.Id, command.HarmonogramHodnoty, scheduleTypeDefinitions, pendingScheduleProposalLock, ct);
        }

        var (tx, ownsTransaction) = await BeginSerializableTransactionIfNeededAsync(ct);
        await using var _ = tx;
        var oldPlanValues = await LoadSchedulePlanValueMapAsync(entity.Id, scheduleTypeDefinitions, ct);
        var oldScheduleSnapshot = await LoadRecordScheduleAuditSnapshotAsync(entity.Id, ct);
        var normalizedScheduleValues = await ReplaceRecordScheduleValuesAsync(entity.Id, valuesToPersist, scheduleTypeDefinitions, ct);
        await dbContext.SaveChangesAsync(ct);
        var newPlanValues = await LoadSchedulePlanValueMapAsync(entity.Id, scheduleTypeDefinitions, ct);
        if (!ScheduleValueMapsEqual(oldPlanValues, newPlanValues))
        {
            await priorityMatrixRebuildService.RebuildForRecordAsync(entity.Id, ct);
        }
        var newScheduleSnapshot = await LoadRecordScheduleAuditSnapshotAsync(entity.Id, ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            oldScheduleSnapshot is null ? AuditActionType.Create : AuditActionType.Update,
            AuditEntityType.RecordSchedule,
            entity.Id.ToString(CultureInfo.InvariantCulture),
            oldScheduleSnapshot,
            newScheduleSnapshot));
        await dbContext.SaveChangesAsync(ct);
        if (ownsTransaction && tx is not null)
        {
            await tx.CommitAsync(ct);
        }

        return entity.Id;
    }

    private async Task EnsureScheduleAddScopeAccessAsync(
        ProjektovyZaznamEntity entity,
        CurrentUserContextViewModel currentUser,
        bool canEditScheduleFull,
        IRecordWriteCommandsComposition composition,
        CancellationToken ct)
    {
        if (canEditScheduleFull)
        {
            return;
        }

        if (currentUser.OsobaId > 0 && entity.VlastnikId == currentUser.OsobaId)
        {
            return;
        }

        var subsystemLeadEquivalentOsobaIds = await composition.ResolveLeadEquivalentOsobaIdsAsync(entity.ProjektId, entity.SubsystemId, ct);
        if (currentUser.OsobaId > 0 && subsystemLeadEquivalentOsobaIds.Contains(currentUser.OsobaId))
        {
            return;
        }

        throw new InvalidOperationException("Nemáte oprávnění doplňovat harmonogram tohoto úkolu.");
    }

    private async Task<List<SaveRecordHarmonogramValueCommand>> BuildScheduleValuesForAddOnlyAsync(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions,
        CancellationToken ct)
    {
        if (scheduleTypeDefinitions.Count == 0)
        {
            return [];
        }

        var durationTypeIds = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var delayTypeIds = scheduleTypeDefinitions
            .Select(x => x.DelayTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var durationTypeSet = durationTypeIds.ToHashSet();
        var allowedTypeIds = durationTypeIds
            .Concat(delayTypeIds)
            .ToHashSet();
        if (allowedTypeIds.Count == 0)
        {
            return [];
        }

        var submittedByType = submittedValues
            .Where(x => allowedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .ToDictionary(
                group => group.Key,
                group => durationTypeSet.Contains(group.Key)
                    ? Math.Max(0, group.Last().Hodnota)
                    : group.Last().Hodnota);

        var existingByType = (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
                .ToListAsync(ct))
            .ToDictionary(
                x => x.TypId,
                x => durationTypeSet.Contains(x.TypId)
                    ? Math.Max(0, x.HodnotaInt)
                    : x.HodnotaInt);

        var desired = new Dictionary<int, int>();

        foreach (var typeId in delayTypeIds)
        {
            if (submittedByType.TryGetValue(typeId, out var delayValue))
            {
                desired[typeId] = delayValue;
            }
            else if (existingByType.TryGetValue(typeId, out var existingDelayValue))
            {
                desired[typeId] = existingDelayValue;
            }
        }

        foreach (var typeId in durationTypeIds)
        {
            var existingDuration = existingByType.GetValueOrDefault(typeId, 0);
            if (existingDuration > 0)
            {
                desired[typeId] = existingDuration;
                continue;
            }

            if (submittedByType.TryGetValue(typeId, out var submittedDuration))
            {
                desired[typeId] = submittedDuration;
            }
        }

        return desired
            .Select(item => new SaveRecordHarmonogramValueCommand
            {
                TypId = item.Key,
                Hodnota = item.Value
            })
            .OrderBy(x => x.TypId)
            .ToList();
    }

    private async Task<List<SaveRecordHarmonogramValueCommand>> BuildScheduleValuesPreservingLockedScopeAsync(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions,
        PendingScheduleProposalLockState lockState,
        CancellationToken ct)
    {
        if (scheduleTypeDefinitions.Count == 0)
        {
            return [];
        }

        var durationTypeIds = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var delayTypeIds = scheduleTypeDefinitions
            .Select(x => x.DelayTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var allowedTypeIds = durationTypeIds
            .Concat(delayTypeIds)
            .ToHashSet();
        if (allowedTypeIds.Count == 0)
        {
            return [];
        }

        if (!lockState.LocksSchedule)
        {
            return submittedValues
                .Where(x => allowedTypeIds.Contains(x.TypId))
                .GroupBy(x => x.TypId)
                .Select(group => new SaveRecordHarmonogramValueCommand
                {
                    TypId = group.Key,
                    Hodnota = group.Last().Hodnota
                })
                .OrderBy(x => x.TypId)
                .ToList();
        }

        var existingByType = (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
                .ToListAsync(ct))
            .ToDictionary(x => x.TypId, x => x.HodnotaInt);
        var submittedByType = submittedValues
            .Where(x => allowedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .ToDictionary(group => group.Key, group => group.Last().Hodnota);

        var desired = new Dictionary<int, int>();

        foreach (var typeId in durationTypeIds)
        {
            if (existingByType.TryGetValue(typeId, out var existingDuration))
            {
                desired[typeId] = Math.Max(0, existingDuration);
            }
        }

        foreach (var typeId in delayTypeIds)
        {
            if (submittedByType.TryGetValue(typeId, out var submittedDelay))
            {
                desired[typeId] = submittedDelay;
            }
            else if (existingByType.TryGetValue(typeId, out var existingDelay))
            {
                desired[typeId] = existingDelay;
            }
        }

        return desired
            .Select(item => new SaveRecordHarmonogramValueCommand
            {
                TypId = item.Key,
                Hodnota = item.Value
            })
            .OrderBy(x => x.TypId)
            .ToList();
    }

    private async Task<List<SaveRecordHarmonogramValueCommand>> ReplaceRecordScheduleValuesAsync(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> harmonogramValues,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions,
        CancellationToken ct)
    {
        var durationTypeSet = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();
        var allowedTypeIds = scheduleTypeDefinitions
            .SelectMany(x => new[] { x.DurationTypeId, x.DelayTypeId })
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();

        if (allowedTypeIds.Count == 0)
        {
            return [];
        }

        var normalized = harmonogramValues
            .Where(x => allowedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .Select(group => new
            {
                TypId = group.Key,
                Hodnota = durationTypeSet.Contains(group.Key)
                    ? Math.Max(0, group.Last().Hodnota)
                    : group.Last().Hodnota
            })
            .Where(x => x.Hodnota != 0)
            .ToDictionary(x => x.TypId, x => x.Hodnota);
        var normalizedResult = normalized
            .Select(item => new SaveRecordHarmonogramValueCommand
            {
                TypId = item.Key,
                Hodnota = item.Value
            })
            .OrderBy(x => x.TypId)
            .ToList();

        var existing = await dbContext.ZaznamHarmonogramHodnoty
            .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var row in existing)
        {
            if (normalized.TryGetValue(row.TypId, out var value))
            {
                row.HodnotaInt = value;
                row.UpdatedAt = now;
                normalized.Remove(row.TypId);
            }
            else
            {
                dbContext.ZaznamHarmonogramHodnoty.Remove(row);
            }
        }

        foreach (var item in normalized)
        {
            dbContext.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                ZaznamId = zaznamId,
                TypId = item.Key,
                HodnotaInt = item.Value,
                UpdatedAt = now
            });
        }

        return normalizedResult;
    }

    private static bool IsMeetingReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
        => MeetingStatePolicy.IsReadOnly(meeting, status);

    private async Task<(IDbContextTransaction? Transaction, bool OwnsTransaction)> BeginSerializableTransactionIfNeededAsync(CancellationToken ct)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return (null, false);
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        return (transaction, true);
    }

    private Task<List<OpenMeetingRow>> LoadOpenProjectMeetingsAsync(int projectId, CancellationToken ct)
        => (
                from meeting in dbContext.Jednani.AsNoTracking()
                join state in dbContext.CiselnikStavuJednani.AsNoTracking() on meeting.StavJednaniId equals state.Id
                where meeting.ProjektId == projectId
                    && !meeting.UzamklOsobaId.HasValue
                    && state.Kod != "CLOSED"
                    && !EF.Functions.Like(state.Nazev, "%uzav%")
                select new OpenMeetingRow(
                    meeting.Id,
                    meeting.CisloJednani))
            .ToListAsync(ct);

    private DateTime GetLocalNow()
        => timeProvider.GetLocalNow().LocalDateTime;

    private async Task<RecordScheduleAuditSnapshot?> LoadRecordScheduleAuditSnapshotAsync(int recordId, CancellationToken ct)
    {
        var rows = await dbContext.ZaznamHarmonogramHodnoty
            .AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderBy(x => x.TypId)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        return rows.Count == 0 ? null : RecordScheduleAuditSnapshot.FromEntities(recordId, rows);
    }

    private async Task<Dictionary<int, int>> LoadSchedulePlanValueMapAsync(
        int recordId,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions,
        CancellationToken ct)
    {
        var durationTypeIds = scheduleTypeDefinitions
            .Select(x => x.DurationTypeId)
            .Where(x => x > 0)
            .Distinct()
            .ToHashSet();
        if (durationTypeIds.Count == 0)
        {
            return [];
        }

        return await dbContext.ZaznamHarmonogramHodnoty
            .AsNoTracking()
            .Where(x => x.ZaznamId == recordId && durationTypeIds.Contains(x.TypId))
            .ToDictionaryAsync(x => x.TypId, x => x.HodnotaInt, ct);
    }

    private static bool ScheduleValueMapsEqual(
        IReadOnlyDictionary<int, int> left,
        IReadOnlyDictionary<int, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var otherValue) || otherValue != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
