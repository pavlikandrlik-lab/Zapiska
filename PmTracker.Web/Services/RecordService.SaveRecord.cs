using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

/// <summary>
/// Fáze 3C Task 1: RecordService.SaveRecord.cs — SaveRecordAsync + private
/// helpers pro validaci, harmonogram persistence, external links, collaborators.
/// Další operace (Delete, MeetingIdentifier) v samostatných partials.
/// </summary>
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
        var authz = currentUser.Authorization ?? throw new InvalidOperationException(
            "AuthorizationSnapshot must be populated for this request; UserContextResolver did not set it.");

        // Per-action redesign 2026-04-23: records.schedule.add byl smazán — nový model má
        // jen records.schedule.edit (plná editace harmonogramu) a records.edit (editace záznamu).
        // Bývalá "append-only" větev je pryč; slot-level write restriction zůstává jako service
        // invariant (harmonogram proposals = doporučený workflow pro non-edit role).
        var canEditRecord = authz.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId);
        var canCreateRecord = authz.HasPermission(PermissionKeys.RecordsCreate, command.ProjektId);
        var canEditScheduleFull = authz.HasPermission(PermissionKeys.RecordsScheduleEdit, command.ProjektId);
        if (!canEditRecord && !canCreateRecord)
        {
            if (!canEditScheduleFull)
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
        var isTaskCategory = validation.IsTaskCategory;
        var project = validation.Project;
        var normalizedCollaborationIds = validation.NormalizedCollaborationIds;
        var pendingScheduleProposalLock = validation.ExistingRecord is not null
            ? await pendingScheduleProposalLockEvaluator.EvaluateAsync(validation.ExistingRecord.Id, ct)
            : new PendingScheduleProposalLockState(false, null, null, false, false);
        var oldRecordSnapshot = validation.ExistingRecord is null
            ? null
            : RecordAuditSnapshot.FromEntity(validation.ExistingRecord);

        return await ExecuteInSerializableTransactionAsync(async innerCt =>
        {
            ProjektovyZaznamEntity entity;
            if (command.Id.HasValue)
            {
                entity = await dbContext.ProjektoveZaznamy
                    .FirstOrDefaultAsync(x => x.Id == command.Id.Value, innerCt)
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

                await dbContext.SaveChangesAsync(innerCt);

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
                        DatumZmeny = timeProvider.GetUtcNow().UtcDateTime
                    });
                }
            }
            else
            {
                var requestedCislo = command.CisloZaznamu > 0 ? command.CisloZaznamu : 0;
                var cislo = requestedCislo;
                if (cislo <= 0
                    || await dbContext.ProjektoveZaznamy.AnyAsync(x => x.ProjektId == command.ProjektId && x.CisloZaznamu == cislo, innerCt))
                {
                    cislo = await composition.GetNextCisloZaznamuTransactionalAsync(command.ProjektId, innerCt);
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
                    var nextOrder = await composition.AllocateMeetingOrderTransactionalAsync(command.ProjektId, meeting.CisloJednani, innerCt);
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
                    SubsystemId = subsystemId
                };
                dbContext.ProjektoveZaznamy.Add(entity);
            }

            await dbContext.SaveChangesAsync(innerCt);
            await ReplaceRecordCollaborationAsync(entity.Id, normalizedCollaborationIds, innerCt);
            var addedExternalLinks = await ReplaceRecordExternalLinksAsync(entity.Id, command.ExterniVazby, innerCt);

            // Datum-model: UPSERT krok rows (plán datumy + manuální skutečnost + rezim).
            // Auto skutečnost řeší harvest (SyncZaznamAsync), ne save.
            _ = await PersistScheduleKrokyAsync(entity, command, isTaskCategory, innerCt);

            await priorityMatrixRebuildService.RebuildForRecordAsync(entity.Id, innerCt);
            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                command.Id.HasValue ? AuditActionType.Update : AuditActionType.Create,
                AuditEntityType.Record,
                entity.Id.ToString(CultureInfo.InvariantCulture),
                oldRecordSnapshot,
                RecordAuditSnapshot.FromEntity(entity)));

            await dbContext.SaveChangesAsync(innerCt);

            // Plán B Task 10: po uložení externích vazeb (kdy mají Id) spustíme harvest.
            // Awaitujeme — enqueue je rychlé a musí skončit před disposalem request scope,
            // jinak by reactive adapter (Plán sd-sync-revise) ztratil DbContext. Používáme
            // CancellationToken.None — harvest běží na pozadí a nesmí být zrušen request ct.
            foreach (var link in addedExternalLinks)
            {
                if (link.Id > 0 && !string.IsNullOrWhiteSpace(link.Cislo))
                {
                    await harvestScheduler.ScheduleHarvestAsync(link.Id, CancellationToken.None);
                }
            }

            return entity.Id;
        }, ct);
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

        await ValidateExternalLinksAsync(command.ExterniVazby, existingRecord, issues, ct);
        await ValidateScheduleValuesAsync(command, isTaskCategory, existingRecord, issues, ct);

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
        ProjektovyZaznamEntity? existingRecord,
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
        var vyzvaRows = await dbContext.Vyzvy.AsNoTracking()
            .Select(x => new { x.Id, x.Kod })
            .ToListAsync(ct);

        // Plán 3 Feature D (2026-04-24, U10): hard constraint — každá NOVĚ
        // přidávaná externí vazba musí mít platné 6-místné HOT_ZAZNAMY.id,
        // SD integrace musí být zapnutá a ticket musí existovat v HOT_ZAZNAMY.
        // Stávající vazby (ty, jejichž Cislo už záznam má) migrace neruší.
        // Memory: project_servicedesk_infosystem_binding.md § „Externí vazba
        // hard constraint" + feedback_sd_ticket_id_required.md (ticket bez id
        // je mimo scope — nikdy fallback na 0).
        var existingCisla = existingRecord is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(
                await dbContext.ZaznamExterniOdkazy.AsNoTracking()
                    .Where(x => x.ZaznamId == existingRecord.Id)
                    .Select(x => x.Cislo)
                    .ToListAsync(ct),
                StringComparer.Ordinal);

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
            // FIX 2026-05-05: 4 datumy odstraněny z empty-row check (form je needituje, harvest spravuje).
            if (!hasType && !hasCislo && string.IsNullOrWhiteSpace(priceValue) && string.IsNullOrWhiteSpace(vyzvaValue))
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
                var vyzvaExists = vyzvaRows.Any(x => string.Equals(x.Kod, vyzvaValue, StringComparison.OrdinalIgnoreCase));
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

            // FIX 2026-05-05: validace ordering 4 datumů odstraněna — datumy jsou READ-ONLY,
            // user je needituje, harvest jediný píše do DB. Pokud SD vrátí "inconsistent" data
            // (plán dodání < datum objednání), je to data z HOT_ZAZNAMY a my jen zobrazujeme.

            // Plán 3 Feature D: SD hard constraint — pouze pro NOVĚ přidávané
            // vazby. Existing links (Cislo už v DB pro tento záznam) nevalidujeme,
            // aby migrace nic nerušila. Format chyba (!hasCislo) už byla výše.
            if (hasCislo && !existingCisla.Contains(cisloValue))
            {
                var sdValidation = await externiOdkazValidator
                    .ValidateCreateAsync(cisloValue, ct)
                    .ConfigureAwait(false);
                if (!sdValidation.IsValid)
                {
                    AddRecordValidationIssue(
                        issues,
                        $"{rowPrefix}.Cislo",
                        sdValidation.ErrorMessage ?? "Externí vazbu nelze ověřit.",
                        "external",
                        $"external_sd_{sdValidation.ErrorCode ?? "unknown"}",
                        cisloValue);
                }
            }
        }
    }

    private async Task ValidateScheduleValuesAsync(
        SaveRecordCommand command,
        bool isTaskCategory,
        ProjektovyZaznamEntity? existingRecord,
        List<RecordValidationIssue> issues,
        CancellationToken ct)
    {
        if (!isTaskCategory || command.HarmonogramHodnoty.Count == 0)
        {
            return;
        }

        // F-11: Soft concurrency check pro harmonogram
        if (!string.IsNullOrEmpty(command.ScheduleVersion) && existingRecord is not null)
        {
            var currentMaxUpdatedAt = await dbContext.ZaznamHarmonogramKroky
                .Where(x => x.ZaznamId == existingRecord.Id)
                .MaxAsync(x => (DateTime?)x.UpdatedAt, ct);

            var currentVersion = currentMaxUpdatedAt.HasValue
                ? currentMaxUpdatedAt.Value.Ticks.ToString("X16")
                : string.Empty;

            if (currentVersion != command.ScheduleVersion)
            {
                AddRecordValidationIssue(
                    issues,
                    "ScheduleVersion",
                    "Harmonogram byl mezitím upraven jiným uživatelem. Načtěte záznam znovu.",
                    "schedule",
                    "schedule_stale_data",
                    null);
                return;
            }
        }

        // Datum-model: validuj poradí 1–10 + duplicitu. Plán/skutečnost jsou datumy bez range omezení.
        var seenPoradi = new HashSet<int>();
        for (var index = 0; index < command.HarmonogramHodnoty.Count; index++)
        {
            var item = command.HarmonogramHodnoty[index];
            var rowPrefix = $"HarmonogramHodnoty[{index}]";

            if (item.Poradi is < 1 or > 10)
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.Poradi",
                    "Pořadí kroku harmonogramu musí být 1–10.",
                    "schedule",
                    "schedule_poradi_invalid",
                    item.Poradi.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (!seenPoradi.Add(item.Poradi))
            {
                AddRecordValidationIssue(
                    issues,
                    $"{rowPrefix}.Poradi",
                    $"Krok {item.Poradi} je zadaný vícekrát.",
                    "schedule",
                    "schedule_poradi_duplicate",
                    item.Poradi.ToString(CultureInfo.InvariantCulture));
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

    /// <summary>
    /// Datum-model UPSERT: zapíše plán datumy (všechny kroky) + manuální skutečnost (kroky 2/5/8/9)
    /// + rezim do <c>zaznam_harmonogram_krok</c>. Auto skutečnost řeší harvest sync, ne save.
    /// </summary>
    private async Task<bool> PersistScheduleKrokyAsync(
        PmTracker.Web.Models.Entities.ProjektovyZaznamEntity entity,
        SaveRecordCommand command,
        bool isTaskCategory,
        CancellationToken ct)
    {
        if (!isTaskCategory)
        {
            var existingNonTask = await dbContext.ZaznamHarmonogramKroky
                .Where(k => k.ZaznamId == entity.Id)
                .ToListAsync(ct);
            if (existingNonTask.Count == 0)
            {
                return false;
            }
            dbContext.ZaznamHarmonogramKroky.RemoveRange(existingNonTask);
            return true;
        }

        if (command.HarmonogramHodnoty.Count == 0 && command.ManualActualKroky.Count == 0)
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        var existing = await dbContext.ZaznamHarmonogramKroky
            .Where(k => k.ZaznamId == entity.Id)
            .ToListAsync(ct);
        var byPoradi = existing
            .GroupBy(k => (int)k.Poradi)
            .ToDictionary(g => g.Key, g => g.First());

        var manualByPoradi = command.ManualActualKroky
            .Where(m => m.Poradi is >= 1 and <= 10)
            .GroupBy(m => m.Poradi)
            .ToDictionary(g => g.Key, g => g.Last());

        var rezimManual = string.Equals(command.HarmonogramRezim, "Manual", StringComparison.OrdinalIgnoreCase);
        var changed = false;

        PmTracker.Web.Models.Entities.ZaznamHarmonogramKrokEntity GetOrCreate(int poradi)
        {
            if (byPoradi.TryGetValue(poradi, out var r))
            {
                return r;
            }
            r = new PmTracker.Web.Models.Entities.ZaznamHarmonogramKrokEntity
            {
                ZaznamId = entity.Id,
                Poradi = (byte)poradi,
                SkutecnostRezim = (byte)PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = nowUtc
            };
            dbContext.ZaznamHarmonogramKroky.Add(r);
            byPoradi[poradi] = r;
            return r;
        }

        foreach (var item in command.HarmonogramHodnoty)
        {
            if (item.Poradi is < 1 or > 10)
            {
                continue;
            }
            var row = GetOrCreate(item.Poradi);
            row.PlanDatum = item.PlanDatum?.Date;
            if (PmTracker.Web.Models.ViewModels.HarmonogramManualSteps.IsManual(item.Poradi))
            {
                DateTime? skut = item.SkutecnostDatum?.Date;
                if (manualByPoradi.TryGetValue(item.Poradi, out var mk) && mk.AbsolutniDatum.HasValue)
                {
                    skut = mk.AbsolutniDatum.Value.ToDateTime(TimeOnly.MinValue);
                }
                row.SkutecnostDatum = skut;
                row.SkutecnostRezim = (byte)PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Manual;
                row.SkutecnostZdroj = skut.HasValue
                    ? (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Manual
                    : (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Neznamo;
            }
            else
            {
                row.SkutecnostRezim = rezimManual
                    ? (byte)PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Manual
                    : (byte)PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Auto;
            }
            row.UpdatedAt = nowUtc;
            changed = true;
        }

        foreach (var mk in manualByPoradi.Values)
        {
            if (!PmTracker.Web.Models.ViewModels.HarmonogramManualSteps.IsManual(mk.Poradi))
            {
                continue;
            }
            var row = GetOrCreate(mk.Poradi);
            row.SkutecnostDatum = mk.AbsolutniDatum?.ToDateTime(TimeOnly.MinValue);
            row.SkutecnostRezim = (byte)PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Manual;
            row.SkutecnostZdroj = mk.AbsolutniDatum.HasValue
                ? (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Manual
                : (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Neznamo;
            row.UpdatedAt = nowUtc;
            changed = true;
        }

        return changed;
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

        return await dbContext.Vyzvy
            .Where(x => x.Kod == value)
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

    /// <summary>
    /// UPSERT logika pro externí vazby.
    ///
    /// Historie:
    /// - 2026-04-27: Naivní DELETE+INSERT (původní impl) selhával FK violation, protože
    ///   <c>zaznam_harmonogram_vyjadreni_vazba.externi_odkaz_id</c> mělo
    ///   <c>FK ... ON DELETE NO ACTION</c>. Fix přidal pre-flight check + UPSERT.
    /// - 2026-04-28 (commit ca2f06c): NES odpojení + 4 datumy + synthetic K1 binding.
    /// - 2026-04-28 (commit 68035e1): pokus o CASCADE FK_zhvv_externi_odkaz (1_3_13).
    /// - 2026-04-28 (REVERTED): User při deploy narazil na SQL 1785 multi-cascade-path:
    ///   <c>vyjadreni_vazby</c> má dva FK na <c>projektove_zaznamy</c> (přes
    ///   <c>zaznam_id</c> direct + přes <c>zaznam_externi_odkazy.zaznam_id</c>),
    ///   takže CASCADE na obou cestách porušil SQL Server constraint o single-path
    ///   cascade graph. Migration 1_3_13 byla odstraněna, FK_zhvv_externi_odkaz
    ///   zůstává <c>NO ACTION</c>. Aplikace explicitně cleanup-uje
    ///   <c>vyjadreni_vazby</c> rows PŘED smazáním externí vazby — pre-flight
    ///   harvest_locked check ODSTRANĚN, delete je nyní běžná operace.
    ///
    /// Logika:
    /// 1. UPDATE existing rows by Id (zachová Id → FK references v vyjadreni_vazby zůstanou platné
    ///    pro PRESERVED vazby).
    /// 2. INSERT nové vazby (Id == 0).
    /// 3. DELETE existing rows co NEJSOU v command — aplikace nejdřív RemoveRange
    ///    navázaných vyjadreni_vazby rows, pak RemoveRange externí vazby
    ///    (EF Core SaveChanges respektuje FK ordering).
    /// </summary>
    private async Task<List<ZaznamExterniOdkazEntity>> ReplaceRecordExternalLinksAsync(int zaznamId, IReadOnlyList<SaveRecordExterniVazbaCommand> externalLinks, CancellationToken ct)
    {
        var existing = await dbContext.ZaznamExterniOdkazy.Where(x => x.ZaznamId == zaznamId).ToListAsync(ct);
        var existingById = existing.ToDictionary(e => e.Id);

        var validLinks = externalLinks
            .Where(l => !string.IsNullOrWhiteSpace(l.Typ) && !string.IsNullOrWhiteSpace(l.Cislo))
            .ToList();

        var commandKeptIds = validLinks
            .Where(l => l.Id > 0)
            .Select(l => l.Id)
            .ToHashSet();

        var toDelete = existing.Where(e => !commandKeptIds.Contains(e.Id)).ToList();

        if (toDelete.Count > 0)
        {
            // FK_zhvv_externi_odkaz je NO ACTION (db_upgrade_1_3_6) — multi-cascade-path
            // brání použít CASCADE (SQL 1785 — vyjadreni_vazby má dva FK na projektove_zaznamy).
            // Aplikace explicitně cleanup-uje navázané vyjadreni_vazby PŘED delete externí vazby.
            // EF Core SaveChanges respektuje FK ordering: nejdřív DELETE z vyjadreni_vazby,
            // pak DELETE z zaznam_externi_odkazy.
            var deleteIds = toDelete.Select(e => e.Id).ToList();
            var bindingsToCleanup = await dbContext.VyjadreniVazby
                .Where(v => deleteIds.Contains(v.ExterniOdkazId))
                .ToListAsync(ct);
            if (bindingsToCleanup.Count > 0)
            {
                dbContext.VyjadreniVazby.RemoveRange(bindingsToCleanup);
            }

            dbContext.ZaznamExterniOdkazy.RemoveRange(toDelete);
        }

        var result = new List<ZaznamExterniOdkazEntity>();
        foreach (var link in validLinks)
        {
            // validLinks už filtroval null/whitespace Typ a Cislo (! je tedy bezpečné)
            var typeId = await ResolveTypOdkazuIdAsync(link.Typ!, ct);
            var price = NormalizeEstimatedExternalLinkPrice(link.Typ, link.PredpokladanaCena);
            var vyzvaId = await ResolveVyzvaIdAsync(link.Vyzva, ct);
            var cislo = link.Cislo!.Trim();

            if (link.Id > 0 && existingById.TryGetValue(link.Id, out var existingEntity))
            {
                // UPDATE in place — Id se nemění, FK references v vyjadreni_vazby zůstávají platné.
                // FIX 2026-05-05: 4 datumy (DatumObjednani/PlanDodani/DatumDodani/DatumPrevzeti)
                // jsou READ-ONLY z perspektivy formuláře. Auto-fill harvest (PerTicketMetadataSyncService)
                // je single source of truth — Save flow je nesmí přepsat z form payloadu, jinak by
                // null z form (žádný input v UI) zničil hodnoty napsané harvesterem.
                existingEntity.TypOdkazuId = typeId;
                existingEntity.Cislo = cislo;
                existingEntity.PredpokladanaCena = price;
                existingEntity.VyzvaId = vyzvaId;
                result.Add(existingEntity);
            }
            else
            {
                // FIX 2026-05-05: nová vazba — 4 datumy zůstanou null. Harvest scheduler
                // (volaný na konci SaveRecord) je dotáhne ze ServiceDesku po commit.
                var entity = new ZaznamExterniOdkazEntity
                {
                    ZaznamId = zaznamId,
                    TypOdkazuId = typeId,
                    Cislo = cislo,
                    PredpokladanaCena = price,
                    VyzvaId = vyzvaId
                };
                dbContext.ZaznamExterniOdkazy.Add(entity);
                result.Add(entity);
            }
        }
        return result;
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

        return await ExecuteInSerializableTransactionAsync(async innerCt =>
        {
            var changed = await PersistScheduleKrokyAsync(entity, command, isTaskCategory: true, innerCt);
            if (changed)
            {
                await priorityMatrixRebuildService.RebuildForRecordAsync(entity.Id, innerCt);
                auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.RecordSchedule,
                    entity.Id.ToString(CultureInfo.InvariantCulture),
                    null,
                    new { Action = "schedule-save" }));
                await dbContext.SaveChangesAsync(innerCt);
            }
            return entity.Id;
        }, ct);
    }

    private async Task<T> ExecuteInSerializableTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(ct);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await operation(ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    private DateTime GetLocalNow()
        => timeProvider.GetLocalNow().LocalDateTime;
}
