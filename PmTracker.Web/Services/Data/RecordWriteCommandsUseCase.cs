using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

public sealed class RecordWriteCommandsUseCase(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    TimeProvider timeProvider) : IRecordWriteCommandsUseCase
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

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser, IRecordWriteCommandsComposition composition)
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

            return SaveRecordScheduleOnly(command, currentUser, canEditScheduleFull, composition);
        }

        var validation = ValidateRecordSaveCommand(command, composition);
        var ownerId = validation.OwnerId;
        var categoryId = validation.KategorieId;
        var statusId = validation.StavUkoluId;
        var typeId = validation.TypUkoluId;
        var subsystemId = validation.SubsystemId;
        var defaultSchemaVersion = validation.DefaultSchemaVersion;
        var isTaskCategory = validation.IsTaskCategory;
        var project = validation.Project;
        var normalizedCollaborationIds = validation.NormalizedCollaborationIds;

        using var tx = dbContext.Database.BeginTransaction(IsolationLevel.Serializable);

        ProjektovyZaznamEntity entity;
        if (command.Id.HasValue)
        {
            entity = dbContext.ProjektoveZaznamy.FirstOrDefault(x => x.Id == command.Id.Value)
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
            entity.DatumUkonceni = command.TerminUkonceni.Date;
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

            dbContext.SaveChanges();

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
                || dbContext.ProjektoveZaznamy.Any(x => x.ProjektId == command.ProjektId && x.CisloZaznamu == cislo))
            {
                cislo = composition.GetNextCisloZaznamuTransactional(command.ProjektId);
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
                var nextOrder = composition.AllocateMeetingOrderTransactional(command.ProjektId, meeting.CisloJednani);
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

        dbContext.SaveChanges();
        ReplaceRecordCollaboration(entity.Id, normalizedCollaborationIds);
        ReplaceRecordExternalLinks(entity.Id, command.ExterniVazby);

        List<SaveRecordHarmonogramValueCommand>? normalizedScheduleValues = null;
        if (isTaskCategory && command.HarmonogramHodnoty.Count > 0)
        {
            var scheduleTypeDefinitions = composition.ResolveScheduleTypeDefinitionsForRecord(entity);
            normalizedScheduleValues = ReplaceRecordScheduleValues(entity.Id, command.HarmonogramHodnoty, scheduleTypeDefinitions);
        }
        else if (!isTaskCategory)
        {
            var existingScheduleValues = dbContext.ZaznamHarmonogramHodnoty
                .Where(x => x.ZaznamId == entity.Id)
                .ToList();
            if (existingScheduleValues.Count > 0)
            {
                dbContext.ZaznamHarmonogramHodnoty.RemoveRange(existingScheduleValues);
                normalizedScheduleValues = [];
            }
        }

        dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(currentUser.OsobaId, "projektove_zaznamy", entity.Id.ToString(CultureInfo.InvariantCulture), command.Id.HasValue ? "update" : "create", null, JsonSerializer.Serialize(entity));
        if (normalizedScheduleValues is not null)
        {
            WriteAudit(
                currentUser.OsobaId,
                "zaznam_harmonogram_hodnoty",
                entity.Id.ToString(CultureInfo.InvariantCulture),
                "upsert",
                null,
                JsonSerializer.Serialize(normalizedScheduleValues));
        }

        return entity.Id;
    }

    public void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser)
    {
        var record = dbContext.ProjektoveZaznamy.FirstOrDefault(x => x.Id == command.ZaznamId)
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");

        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        using var tx = dbContext.Database.BeginTransaction(IsolationLevel.Serializable);

        var historyTypeRows = dbContext.ZaznamHistorieZmenTypu.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var historyDeadlineRows = dbContext.ZaznamHistorieTerminu.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var historyOwnerRows = dbContext.ZaznamHistorieVlastnik.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var historySubsystemRows = dbContext.ZaznamHistorieSubsystem.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var historyStateRows = dbContext.ZaznamHistorieStavuZaznamu.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var historyProjectStateRows = dbContext.ZaznamHistorieStavuProjektu.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var externalLinkRows = dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var collaborationRows = dbContext.ZaznamSpoluprace.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var scheduleRows = dbContext.ZaznamHarmonogramHodnoty.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var commentRows = dbContext.Vyjadreni.Where(x => x.ZaznamId == command.ZaznamId).ToList();
        var oldRecord = JsonSerializer.Serialize(record);

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

        if (historyTypeRows.Count > 0
            || historyDeadlineRows.Count > 0
            || historyOwnerRows.Count > 0
            || historySubsystemRows.Count > 0
            || historyStateRows.Count > 0
            || historyProjectStateRows.Count > 0
            || externalLinkRows.Count > 0
            || collaborationRows.Count > 0
            || scheduleRows.Count > 0
            || commentRows.Count > 0)
        {
            dbContext.SaveChanges();
        }

        dbContext.ProjektoveZaznamy.Remove(record);
        dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(
            currentUser.OsobaId,
            "projektove_zaznamy",
            command.ZaznamId.ToString(CultureInfo.InvariantCulture),
            "hard_delete",
            oldRecord,
            JsonSerializer.Serialize(new
            {
                DeletedComments = commentRows.Count,
                DeletedExternalLinks = externalLinkRows.Count,
                DeletedCollaborationRows = collaborationRows.Count,
                DeletedScheduleRows = scheduleRows.Count,
                DeletedTypeHistoryRows = historyTypeRows.Count,
                DeletedDeadlineHistoryRows = historyDeadlineRows.Count,
                DeletedOwnerHistoryRows = historyOwnerRows.Count,
                DeletedSubsystemHistoryRows = historySubsystemRows.Count,
                DeletedStateHistoryRows = historyStateRows.Count,
                DeletedProjectStateHistoryRows = historyProjectStateRows.Count
            }));
    }

    public void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser, IRecordWriteCommandsComposition composition)
    {
        using var tx = dbContext.Database.BeginTransaction(IsolationLevel.Serializable);

        var record = dbContext.ProjektoveZaznamy
            .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE id = {0}", command.ZaznamId)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Záznam {command.ZaznamId} nebyl nalezen.");
        if (record.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            throw new InvalidOperationException("Záznam už má identifikátor podle jednání.");
        }

        var meetingRows = dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == command.ProjektId)
            .ToList();
        var meetingStateById = dbContext.CiselnikStavuJednani.AsNoTracking()
            .ToDictionary(x => x.Id);
        var openMeetingIds = meetingRows
            .Where(x => !IsMeetingReadOnly(x, meetingStateById.GetValueOrDefault(x.StavJednaniId)))
            .Select(x => x.Id)
            .ToHashSet();
        if (openMeetingIds.Count == 0)
        {
            throw new InvalidOperationException("Není dostupné žádné neuzavřené jednání.");
        }

        var meeting = meetingRows
            .FirstOrDefault(x => x.Id == command.JednaniId)
            ?? throw new InvalidOperationException("Vybrané jednání neexistuje.");
        if (!openMeetingIds.Contains(meeting.Id))
        {
            throw new InvalidOperationException("Vybrané jednání je uzavřené. Vyberte neuzavřené jednání.");
        }

        var nextOrder = composition.AllocateMeetingOrderTransactional(command.ProjektId, meeting.CisloJednani);
        var old = JsonSerializer.Serialize(record);
        record.CisloViditelneTyp = RecordDisplayNumberTypeMeeting;
        record.CisloViditelneA = meeting.CisloJednani;
        record.CisloViditelneB = nextOrder;
        record.CisloJednaniZdrojId = meeting.Id;
        record.CisloViditelne = $"{meeting.CisloJednani}-{nextOrder}";
        dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(
            currentUser.OsobaId,
            "projektove_zaznamy",
            record.Id.ToString(CultureInfo.InvariantCulture),
            "assign_meeting_identifier",
            old,
            JsonSerializer.Serialize(record));
    }

    private SaveRecordValidationContext ValidateRecordSaveCommand(
        SaveRecordCommand command,
        IRecordWriteCommandsComposition composition)
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
        var categoryId = TryResolveForValidation(
            issues,
            "Kategorie",
            "basic",
            "category_not_found",
            command.Kategorie,
            () => ResolveKategorieId(command.Kategorie));
        var statusId = TryResolveForValidation(
            issues,
            "Stav",
            "basic",
            "task_status_not_found",
            command.Stav,
            () => ResolveStavUkoluId(command.Stav));
        var subsystemId = TryResolveForValidation(
            issues,
            "Subsystem",
            "basic",
            "subsystem_invalid_or_inactive",
            command.Subsystem,
            () => ResolveProjectSubsystemId(command.ProjektId, command.Subsystem));

        int? typeId = null;
        if (!string.IsNullOrWhiteSpace(command.TypUkolu))
        {
            typeId = ResolveTypUkoluId(command.TypUkolu);
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
            ? dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
                .Where(x => x.Id == categoryId.Value)
                .Select(x => new { x.Kod, x.Nazev })
                .FirstOrDefault()
            : null;
        var isTaskCategory = IsTaskCategory(category?.Kod, category?.Nazev);
        var defaultSchemaVersion = composition.EnsurePersistedActiveHarmonogramSchemaVersion();

        var project = dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == command.ProjektId)
            .Select(x => new SaveRecordProjectContext(x.Id, x.PouzivatIdentJednani))
            .FirstOrDefault();
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
            ? dbContext.ProjektoveZaznamy.AsNoTracking().FirstOrDefault(x => x.Id == command.Id.Value)
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
            var meetingRows = dbContext.Jednani.AsNoTracking()
                .Where(x => x.ProjektId == command.ProjektId)
                .ToList();
            var meetingStateById = dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);
            var openMeetings = meetingRows
                .Where(row => !IsMeetingReadOnly(row, meetingStateById.GetValueOrDefault(row.StavJednaniId)))
                .ToList();

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
                var selectedMeeting = meetingRows.FirstOrDefault(x => x.Id == command.JednaniIdProCislo.Value);
                if (selectedMeeting is null)
                {
                    AddRecordValidationIssue(
                        issues,
                        "JednaniIdProCislo",
                        "Vybrané jednání neexistuje.",
                        "basic",
                        "meeting_not_found",
                        command.JednaniIdProCislo.Value.ToString(CultureInfo.InvariantCulture));
                }
                else if (openMeetings.All(x => x.Id != selectedMeeting.Id))
                {
                    AddRecordValidationIssue(
                        issues,
                        "JednaniIdProCislo",
                        "Vybrané jednání je uzavřené. Vyberte neuzavřené jednání.",
                        "basic",
                        "meeting_closed",
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
            var allowedOwnerIds = composition.BuildRecordOwnerCandidates(command.ProjektId, existingRecord?.VlastnikId)
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

            var allowedCollaborationIds = composition.BuildRecordOwnerCandidates(command.ProjektId, null)
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

        ValidateExternalLinks(command.ExterniVazby, issues);
        ValidateScheduleValues(command, isTaskCategory, existingRecord, defaultSchemaVersion, composition, issues);

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

    private static int? TryResolveForValidation(
        List<RecordValidationIssue> issues,
        string fieldKey,
        string tab,
        string rule,
        string? value,
        Func<int> resolver)
    {
        try
        {
            return resolver();
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

    private void ValidateExternalLinks(
        IReadOnlyList<SaveRecordExterniVazbaCommand> links,
        List<RecordValidationIssue> issues)
    {
        if (links.Count == 0)
        {
            return;
        }

        var typeRows = dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToList();
        var vyzvaRows = dbContext.CiselnikVyzvy.AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToList();

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

    private void ValidateScheduleValues(
        SaveRecordCommand command,
        bool isTaskCategory,
        ProjektovyZaznamEntity? existingRecord,
        int defaultSchemaVersion,
        IRecordWriteCommandsComposition composition,
        List<RecordValidationIssue> issues)
    {
        if (!isTaskCategory || command.HarmonogramHodnoty.Count == 0)
        {
            return;
        }

        IReadOnlyList<RecordScheduleTypeDefinition> typeDefinitions;
        try
        {
            typeDefinitions = existingRecord is not null && existingRecord.HarmonogramSablonaVerze > 0
                ? composition.ResolveScheduleTypeDefinitionsForSchemaVersion(existingRecord.HarmonogramSablonaVerze)
                : composition.ResolveScheduleTypeDefinitionsForSchemaVersion(defaultSchemaVersion);
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

    private int ResolveKategorieId(string value)
        => dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Kategorie '{value}' neexistuje.");

    private static bool IsTaskCategory(string? categoryCode, string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode)
            && (Ci.Equals(categoryCode.Trim(), "U") || Ci.Equals(categoryCode.Trim(), "UKOL")))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        return categoryName.Contains("úkol", StringComparison.OrdinalIgnoreCase)
            || categoryName.Contains("ukol", StringComparison.OrdinalIgnoreCase);
    }

    private int ResolveStavUkoluId(string value)
        => dbContext.CiselnikStavuUkolu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Stav úkolu '{value}' neexistuje.");

    private int ResolveSubsystemId(string value)
        => dbContext.Subsystemy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Subsystém '{value}' neexistuje.");

    private int ResolveProjectSubsystemId(int projektId, string value)
    {
        var subsystemId = ResolveSubsystemId(value);
        var isActiveInProject = dbContext.ProjektSubsystemy.AsNoTracking()
            .Any(x => x.ProjektId == projektId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue);
        if (!isActiveInProject)
        {
            throw new InvalidOperationException("Vybraný subsystém není aktivně přiřazený projektu.");
        }

        return subsystemId;
    }

    private int ResolveTypOdkazuId(string value)
        => dbContext.CiselnikTypuExternichOdkazu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
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

    private int? ResolveTypUkoluId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return dbContext.CiselnikTypuUkolu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private int? ResolveVyzvaId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return dbContext.CiselnikVyzvy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }

    private void ReplaceRecordCollaboration(int zaznamId, IReadOnlyList<int> selectedPersonIds)
    {
        var existing = dbContext.ZaznamSpoluprace.Where(x => x.ZaznamId == zaznamId).ToList();
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

    private void ReplaceRecordExternalLinks(int zaznamId, IReadOnlyList<SaveRecordExterniVazbaCommand> externalLinks)
    {
        var existing = dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == zaznamId).ToList();
        dbContext.ZaznamExterniOdkazy.RemoveRange(existing);

        foreach (var link in externalLinks)
        {
            if (string.IsNullOrWhiteSpace(link.Typ) || string.IsNullOrWhiteSpace(link.Cislo))
            {
                continue;
            }

            var typeId = ResolveTypOdkazuId(link.Typ);
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
                Vyzva = ResolveVyzvaId(link.Vyzva)
            });
        }
    }

    private int SaveRecordScheduleOnly(
        SaveRecordCommand command,
        CurrentUserContextViewModel currentUser,
        bool canEditScheduleFull,
        IRecordWriteCommandsComposition composition)
    {
        if (!command.Id.HasValue || command.Id.Value <= 0)
        {
            throw new InvalidOperationException("Bez oprávnění records.edit nelze zakládat nový záznam.");
        }

        if (!string.Equals(command.EditorTab, "schedule", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Bez oprávnění records.edit lze ukládat pouze záložku Harmonogram.");
        }

        var entity = dbContext.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefault(x => x.Id == command.Id.Value)
            ?? throw new InvalidOperationException($"Záznam {command.Id.Value} nebyl nalezen.");
        if (entity.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Záznam nepatří do zvoleného projektu.");
        }

        var category = dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == entity.KategorieId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefault();
        if (!IsTaskCategory(category?.Kod, category?.Nazev))
        {
            throw new InvalidOperationException("Harmonogram lze upravovat pouze u záznamů kategorie úkol.");
        }

        EnsureScheduleAddScopeAccess(entity, currentUser, canEditScheduleFull, composition);

        var scheduleTypeDefinitions = composition.ResolveScheduleTypeDefinitionsForRecord(entity);
        var valuesToPersist = canEditScheduleFull
            ? command.HarmonogramHodnoty
            : BuildScheduleValuesForAddOnly(entity.Id, command.HarmonogramHodnoty, scheduleTypeDefinitions);

        using var tx = dbContext.Database.BeginTransaction(IsolationLevel.Serializable);
        var normalizedScheduleValues = ReplaceRecordScheduleValues(entity.Id, valuesToPersist, scheduleTypeDefinitions);
        dbContext.SaveChanges();
        tx.Commit();

        WriteAudit(
            currentUser.OsobaId,
            "zaznam_harmonogram_hodnoty",
            entity.Id.ToString(CultureInfo.InvariantCulture),
            "upsert",
            null,
            JsonSerializer.Serialize(normalizedScheduleValues));

        return entity.Id;
    }

    private void EnsureScheduleAddScopeAccess(
        ProjektovyZaznamEntity entity,
        CurrentUserContextViewModel currentUser,
        bool canEditScheduleFull,
        IRecordWriteCommandsComposition composition)
    {
        if (canEditScheduleFull)
        {
            return;
        }

        if (currentUser.OsobaId > 0 && entity.VlastnikId == currentUser.OsobaId)
        {
            return;
        }

        var subsystemLeadEquivalentOsobaIds = composition.ResolveLeadEquivalentOsobaIds(entity.ProjektId, entity.SubsystemId);
        if (currentUser.OsobaId > 0 && subsystemLeadEquivalentOsobaIds.Contains(currentUser.OsobaId))
        {
            return;
        }

        throw new InvalidOperationException("Nemáte oprávnění doplňovat harmonogram tohoto úkolu.");
    }

    private List<SaveRecordHarmonogramValueCommand> BuildScheduleValuesForAddOnly(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> submittedValues,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions)
    {
        if (scheduleTypeDefinitions.Count == 0)
        {
            return new List<SaveRecordHarmonogramValueCommand>();
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
            return new List<SaveRecordHarmonogramValueCommand>();
        }

        var submittedByType = submittedValues
            .Where(x => allowedTypeIds.Contains(x.TypId))
            .GroupBy(x => x.TypId)
            .ToDictionary(
                group => group.Key,
                group => durationTypeSet.Contains(group.Key)
                    ? Math.Max(0, group.Last().Hodnota)
                    : group.Last().Hodnota);

        var existingByType = dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
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

    private List<SaveRecordHarmonogramValueCommand> ReplaceRecordScheduleValues(
        int zaznamId,
        IReadOnlyList<SaveRecordHarmonogramValueCommand> harmonogramValues,
        IReadOnlyList<RecordScheduleTypeDefinition> scheduleTypeDefinitions)
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
            return new List<SaveRecordHarmonogramValueCommand>();
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

        var existing = dbContext.ZaznamHarmonogramHodnoty
            .Where(x => x.ZaznamId == zaznamId && allowedTypeIds.Contains(x.TypId))
            .ToList();

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

    private DateTime GetLocalNow()
        => timeProvider.GetLocalNow().LocalDateTime;

    private void WriteAudit(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }
}
