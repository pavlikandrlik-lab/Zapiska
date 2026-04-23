using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class RecordProposalService
{
    public async Task<bool> CanViewProposalTabAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var access = await _authorizationPolicy.EvaluateProjectAccessAsync(projectId, currentUser, ct);
        return access.CanViewTab;
    }

    public async Task<ProjektNavrhyTabViewModel> BuildProjectProposalsTabAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var access = await _authorizationPolicy.EvaluateProjectAccessAsync(projectId, currentUser, ct);
        if (!access.CanViewTab)
        {
            throw new InvalidOperationException("Nemáte přístup k návrhům v tomto projektu.");
        }

        var visibleProposalQuery = _dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ProjektId == projectId);
        if (!access.CanDecide)
        {
            visibleProposalQuery = visibleProposalQuery.Where(x => x.CreatedByOsobaId == currentUser.OsobaId);
        }

        var proposals = await visibleProposalQuery
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProposalListRow(
                x.Id,
                x.ProjektId,
                x.ZaznamId,
                x.SubsystemId,
                x.TypNavrhu,
                x.Stav,
                x.PayloadJson,
                x.CreatedByOsobaId,
                x.CreatedAt,
                x.DecidedByOsobaId,
                x.DecidedAt,
                x.ApprovedRecordId))
            .ToListAsync(ct);

        var personIds = proposals
            .Select(x => x.CreatedByOsobaId)
            .Concat(proposals.Where(x => x.DecidedByOsobaId.HasValue).Select(x => x.DecidedByOsobaId!.Value))
            .Distinct()
            .ToList();
        var peopleById = await _dbContext.Osoby.AsNoTracking()
            .Where(x => personIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                Display = string.IsNullOrWhiteSpace(x.Titul)
                    ? $"{x.Jmeno} {x.Prijmeni}"
                    : $"{x.Titul} {x.Jmeno} {x.Prijmeni}"
            })
            .ToDictionaryAsync(x => x.Id, x => x.Display, ct);
        var subsystemIds = proposals.Select(x => x.SubsystemId).Distinct().ToList();
        var subsystemById = await _dbContext.Subsystemy.AsNoTracking()
            .Where(x => subsystemIds.Contains(x.Id))
            .Select(x => new { x.Id, Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}" })
            .ToDictionaryAsync(x => x.Id, x => x.Label, ct);
        var recordRowsById = await _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.Id > 0)
            .Select(x => new ProposalRecordRow(
                x.Id,
                x.CisloViditelne ?? string.Empty,
                x.Nazev,
                x.Cil,
                x.DatumZalozeni,
                x.DatumUkonceni))
            .ToDictionaryAsync(x => x.Id, x => x, ct);

        var items = proposals
            .Select(proposal => BuildProposalListItem(proposal, currentUser, access.CanDecide, peopleById, subsystemById, recordRowsById))
            .ToList();

        return new ProjektNavrhyTabViewModel
        {
            ProjektId = projectId,
            CanCreateRecordProposal = access.CreatableSubsystemIds.Count > 0,
            CanCreateScheduleProposal = access.CreatableSubsystemIds.Count > 0,
            NavrhyZalozeni = items
                .Where(x => string.Equals(x.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
                .ToList(),
            NavrhyHarmonogramu = items
                .Where(x => string.Equals(x.TypNavrhu, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase))
                .ToList()
        };
    }

    public async Task<ZaznamEditViewModel> BuildCreateRecordProposalEditorAsync(int projectId, CurrentUserContextViewModel currentUser, int? meetingId = null, CancellationToken ct = default)
    {
        var access = await _authorizationPolicy.EvaluateProjectAccessAsync(projectId, currentUser, ct);
        if (access.CreatableSubsystemIds.Count == 0)
        {
            throw new InvalidOperationException("Návrh založení záznamu může vytvořit jen vedoucí subsystému nebo jeho zástupce.");
        }

        var model = await _recordService.BuildZaznamCreateAsync(projectId, meetingId, ct);
        await FilterCreateProposalSubsystemsAsync(model, access.CreatableSubsystemIds, ct);
        ConfigureCreateProposalEditor(model);
        return model;
    }

    public async Task<ZaznamEditViewModel> BuildScheduleProposalEditorAsync(int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var access = await _authorizationPolicy.EvaluateRecordAccessAsync(projectId, recordId, currentUser, ct);
        if (!access.CanCreateScheduleProposal)
        {
            throw new InvalidOperationException("Návrh změny termínu a harmonogramu může vytvořit jen vedoucí relevantního subsystému nebo jeho zástupce.");
        }

        var pendingLock = await _pendingScheduleProposalLockEvaluator.EvaluateAsync(recordId, ct);
        if (pendingLock.HasPendingProposal)
        {
            throw new InvalidOperationException(pendingLock.Message ?? "Pro tento záznam už existuje čekající návrh změny harmonogramu.");
        }

        var model = await _recordService.BuildZaznamEditAsync(recordId, ct);
        if (model.ProjektId != projectId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        ConfigureScheduleProposalEditor(model);
        return model;
    }

    public async Task<ZaznamEditViewModel> BuildProposalDetailAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadProposalAsync(projectId, proposalId, ct);
        await EnsureCanViewProposalAsync(proposal, currentUser, ct);

        if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
        {
            var payload = DeserializePayload(proposal.PayloadJson);
            var createPayload = payload.CreateRecord
                ?? throw new InvalidOperationException("Payload návrhu založení záznamu je neplatný.");
            var model = await _recordService.BuildZaznamCreateAsync(projectId, createPayload.JednaniIdProCislo, ct);
            _payloadMapper.ApplyCreatePayload(model, createPayload);
            var canDecide = await _authorizationPolicy.CanDecideProjectProposalAsync(projectId, currentUser, ct);
            ConfigureProposalDetailEditor(model, proposal, canDecide, proposalSummaryNote: "Detail návrhu je jen pro posouzení. Změny návrhu zde neupravujete přímo.");
            return model;
        }

        if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase))
        {
            if (!proposal.ZaznamId.HasValue)
            {
                throw new InvalidOperationException("Návrh změny termínu a harmonogramu není navázán na záznam.");
            }

            var model = await _recordService.BuildZaznamEditAsync(proposal.ZaznamId.Value, ct);
            var payload = DeserializePayload(proposal.PayloadJson);
            var schedulePayload = payload.SchedulePlan
                ?? throw new InvalidOperationException("Payload návrhu harmonogramu je neplatný.");
            var fieldTooltips = BuildScheduleProposalFieldDiffTooltips(model, schedulePayload);
            var scheduleTypeTooltips = BuildScheduleProposalScheduleDiffTooltips(model, schedulePayload);
            ApplySchedulePayloadToModel(model, schedulePayload);
            var canDecide = await _authorizationPolicy.CanDecideProjectProposalAsync(projectId, currentUser, ct);
            ConfigureProposalDetailEditor(
                model,
                proposal,
                canDecide,
                fieldTooltips,
                scheduleTypeTooltips,
                "Zvýrazněná pole ukazují hodnoty navržené ke schválení. Po najetí se zobrazí původní a navržená hodnota.");
            return model;
        }

        throw new InvalidOperationException("Neznámý typ návrhu.");
    }

    public async Task<ZaznamEditViewModel> BuildEditableRecordEditorFromProposalAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadProposalAsync(projectId, proposalId, ct);
        if (!await _authorizationPolicy.CanDecideProjectProposalAsync(projectId, currentUser, ct))
        {
            throw new InvalidOperationException("Převzetí návrhu do formuláře je dostupné jen projektovému manažerovi nebo administrátorovi projektu.");
        }

        if (string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
        {
            return await BuildPrefilledCreateRecordEditorFromProposalAsync(projectId, proposalId, currentUser, ct);
        }

        if (!string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Převzetí návrhu do formuláře je dostupné jen pro podporované typy návrhů.");
        }

        if (!proposal.ZaznamId.HasValue)
        {
            throw new InvalidOperationException("Návrh změny harmonogramu není navázán na záznam.");
        }

        var payload = DeserializePayload(proposal.PayloadJson);
        var schedulePayload = payload.SchedulePlan
            ?? throw new InvalidOperationException("Payload návrhu harmonogramu je neplatný.");
        var model = await _recordService.BuildZaznamEditAsync(proposal.ZaznamId.Value, ct);
        ApplySchedulePayloadToModel(model, schedulePayload);
        ConfigureStandardTakenOverScheduleEditor(model);
        return model;
    }

    public async Task<ZaznamEditViewModel> BuildPrefilledCreateRecordEditorFromProposalAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var proposal = await LoadProposalAsync(projectId, proposalId, ct);
        if (!string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Předvyplnění formuláře je dostupné jen pro návrhy založení záznamu.");
        }

        if (!await _authorizationPolicy.CanDecideProjectProposalAsync(projectId, currentUser, ct))
        {
            throw new InvalidOperationException("Předvyplnění formuláře z návrhu je dostupné jen projektovému manažerovi nebo administrátorovi projektu.");
        }

        var payload = DeserializePayload(proposal.PayloadJson);
        var createPayload = payload.CreateRecord
            ?? throw new InvalidOperationException("Payload návrhu založení záznamu je neplatný.");

        var model = await _recordService.BuildZaznamCreateAsync(projectId, createPayload.JednaniIdProCislo, ct);
        _payloadMapper.ApplyCreatePayload(model, createPayload);
        ConfigureStandardPrefilledCreateEditor(model);
        return model;
    }

    // --- Private helpers used only by queries ---

    private async Task FilterCreateProposalSubsystemsAsync(ZaznamEditViewModel model, IReadOnlySet<int> allowedSubsystemIds, CancellationToken ct)
    {
        var allowedSubsystemRows = await _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == model.ProjektId && !x.DatumOdebrani.HasValue && allowedSubsystemIds.Contains(x.SubsystemId))
            .Join(
                _dbContext.Subsystemy.AsNoTracking(),
                mapping => mapping.SubsystemId,
                subsystem => subsystem.Id,
                (mapping, subsystem) => new
                {
                    subsystem.Id,
                    subsystem.Kod,
                    subsystem.Nazev
                })
            .ToListAsync(ct);

        var allowedKeys = allowedSubsystemRows
            .SelectMany(x => new[] { x.Kod, x.Nazev })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        model.Subsystemy = model.Subsystemy
            .Where(option => allowedKeys.Contains(option.Kod) || allowedKeys.Contains(option.Nazev))
            .ToList();

        if (model.Subsystemy.Count == 0)
        {
            throw new InvalidOperationException("Pro aktuálního uživatele není v projektu dostupný žádný relevantní subsystém pro návrh záznamu.");
        }

        if (!model.Subsystemy.Any(option =>
                string.Equals(option.Kod, model.Subsystem, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.Nazev, model.Subsystem, StringComparison.OrdinalIgnoreCase)))
        {
            model.Subsystem = model.Subsystemy[0].Kod;
        }
    }

    private void ConfigureCreateProposalEditor(ZaznamEditViewModel model)
    {
        model.PageTitle = "Navrhnout založení záznamu";
        model.ModalTitle = "Nový návrh založení záznamu";
        model.PrimaryActionLabel = model.Presentation.Equals("page", StringComparison.OrdinalIgnoreCase)
            ? "Odeslat návrh a vrátit se do projektu"
            : "Odeslat návrh";
        model.FormController = "Navrhy";
        model.FormAction = "SubmitCreateProposal";
        model.ProposalEditorMode = RecordProposalEditorModes.CreateProposal;
        model.SecondaryNote = "Návrh se uloží ke schválení. Provozní záznam vznikne až po schválení projektovým manažerem nebo administrátorem projektu.";
    }

    private void ConfigureScheduleProposalEditor(ZaznamEditViewModel model)
    {
        model.PageTitle = $"Navrhnout změnu termínu a harmonogramu záznamu {model.CisloViditelne}";
        model.ModalTitle = $"Navrhnout změnu termínu a harmonogramu #{model.CisloViditelne}";
        model.PrimaryActionLabel = model.Presentation.Equals("page", StringComparison.OrdinalIgnoreCase)
            ? "Odeslat návrh a vrátit se do projektu"
            : "Odeslat návrh";
        model.FormController = "Navrhy";
        model.FormAction = "SubmitScheduleProposal";
        model.ProposalEditorMode = RecordProposalEditorModes.SchedulePlanProposal;
        model.AllowBasicMetadataEdit = false;
        model.AllowTermDeadlineEdit = true;
        model.CanEditRecord = false;
        model.CanEditScheduleFull = true;
        model.CanEditScheduleAddOnly = false;
        model.ShowExternalTab = false;
        model.ShowCollaborationTab = false;
        model.HarmonogramBlok = CloneScheduleBlock(
            model.HarmonogramBlok,
            permissions: ScheduleEditorPermissionSet.ForFullEdit(model.HarmonogramBlok.Permissions.IsTaskCategory));
        model.ActiveEditorTab = "schedule";
        model.SecondaryNote = "V návrhu lze měnit termín ukončení a celý harmonogram. Ostatní metadata záznamu zůstávají jen pro čtení.";
    }

    private void ConfigureStandardPrefilledCreateEditor(ZaznamEditViewModel model)
    {
        model.PageTitle = "Nový projektový záznam";
        model.ModalTitle = "Nový projektový záznam";
        model.PrimaryActionLabel = model.Presentation.Equals("page", StringComparison.OrdinalIgnoreCase)
            ? "Založit a vrátit se do projektu"
            : "Založit záznam";
        model.FormController = "Zaznamy";
        model.FormAction = "Save";
        model.ProposalEditorMode = RecordProposalEditorModes.None;
        model.SecondaryNote = "Formulář je předvyplněný daty z vybraného návrhu. Po uložení vznikne běžný provozní záznam.";
    }

    private void ConfigureStandardTakenOverScheduleEditor(ZaznamEditViewModel model)
    {
        model.PageTitle = $"Upravit záznam {model.CisloViditelne}";
        model.ModalTitle = $"Upravit záznam #{model.CisloViditelne}";
        model.PrimaryActionLabel = model.Presentation.Equals("page", StringComparison.OrdinalIgnoreCase)
            ? "Uložit a vrátit se do projektu"
            : "Uložit";
        model.FormController = "Zaznamy";
        model.FormAction = "Save";
        model.ProposalEditorMode = RecordProposalEditorModes.None;
        model.SecondaryNote = "Formulář je předvyplněný daty zamítnutého návrhu. Po uložení se promítne běžná úprava záznamu.";
        model.CanEditRecord = true;
        model.CanEditScheduleFull = true;
        model.AllowBasicMetadataEdit = true;
        model.AllowTermDeadlineEdit = true;
        model.ActiveEditorTab = "basic";
        model.HarmonogramBlok = CloneScheduleBlock(
            model.HarmonogramBlok,
            permissions: ScheduleEditorPermissionSet.ForFullEdit(model.HarmonogramBlok.Permissions.IsTaskCategory));
    }

    private void ApplySchedulePayloadToModel(ZaznamEditViewModel model, SchedulePlanProposalPayload payload)
    {
        var valueByType = payload.PlannedHarmonogramHodnoty
            .Concat(payload.ActualHarmonogramHodnoty)
            .GroupBy(value => value.TypId)
            .ToDictionary(group => group.Key, group => group.Last().Hodnota);
        var schema = model.HarmonogramBlok.Kroky
            .OrderBy(step => step.KrokIndex)
            .Select(step => new HarmonogramTypPar(
                step.KrokIndex,
                $"STEP_{step.KrokIndex}_DURATION",
                step.Nazev,
                step.BarvaHex,
                step.TrvaniTypId,
                step.ZpozdeniTypId))
            .ToList();
        var vypocet = _harmonogramService.BuildHarmonogramVypocetPublic(model.DatumZalozeni, schema, valueByType);
        var souhrn = _harmonogramService.BuildHarmonogramSouhrn(vypocet, payload.TerminUkonceni);
        // Zachovat Plán D VM properties z originálního bloku, pokud je nová vypocet neposkytuje.
        // Originál přišel z ProjectService.RecordEditorComposition a má KrokKey, ZdrojSkutecnosti
        // a IsManualKrok naplněné; rebuild po přepočtu je ztratí, pokud je explicitně nepropáčeme.
        var originalByKrokIndex = model.HarmonogramBlok.Kroky.ToDictionary(k => k.KrokIndex);
        var kroky = vypocet.Select(krok =>
        {
            var original = originalByKrokIndex.GetValueOrDefault(krok.KrokIndex);
            return new HarmonogramKrokEditViewModel
            {
                KrokIndex = krok.KrokIndex,
                Nazev = krok.Nazev,
                BarvaHex = krok.BarvaHex,
                TrvaniTypId = krok.TrvaniTypId,
                ZpozdeniTypId = krok.ZpozdeniTypId,
                TrvaniDni = krok.TrvaniDni,
                OdchylkaDni = krok.ZpozdeniDni,
                BaselineDatum = krok.BaselineDatum,
                SkutecneDatum = krok.PosunuteDatum,
                // Plán D Task 8/9 passthrough (M-1 fix).
                KrokKey = original?.KrokKey ?? Guid.Empty,
                ZdrojSkutecnosti = original?.ZdrojSkutecnosti ?? ZdrojSkutecnosti.None,
                SourceVyjadreniId = original?.SourceVyjadreniId,
                SourceVyjadreniDatum = original?.SourceVyjadreniDatum,
                SourceExterniOdkazId = original?.SourceExterniOdkazId,
                IsManualKrok = original?.IsManualKrok ?? HarmonogramManualSteps.IsManual(krok.KrokIndex)
            };
        }).ToList();

        model.TerminUkonceni = payload.TerminUkonceni;
        model.HarmonogramBlok = CloneScheduleBlock(
            model.HarmonogramBlok,
            souhrn: souhrn,
            kroky: kroky,
            terminUkonceni: payload.TerminUkonceni);
    }

    private static IReadOnlyDictionary<string, string> BuildScheduleProposalFieldDiffTooltips(
        ZaznamEditViewModel originalModel,
        SchedulePlanProposalPayload payload)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (payload.ChangesTermDeadline && originalModel.TerminUkonceni.HasValue)
        {
            result["TerminUkonceni"] =
                $"Původní hodnota: {originalModel.TerminUkonceni.Value:dd.MM.yyyy} | Navržená hodnota: {payload.TerminUkonceni:dd.MM.yyyy}";
        }

        return result;
    }

    private static IReadOnlyDictionary<int, string> BuildScheduleProposalScheduleDiffTooltips(
        ZaznamEditViewModel originalModel,
        SchedulePlanProposalPayload payload)
    {
        var result = new Dictionary<int, string>();
        var originalByDurationType = originalModel.HarmonogramBlok.Kroky.ToDictionary(step => step.TrvaniTypId, step => step.TrvaniDni);
        var originalByDelayType = originalModel.HarmonogramBlok.Kroky.ToDictionary(step => step.ZpozdeniTypId, step => step.OdchylkaDni);

        foreach (var planned in payload.PlannedHarmonogramHodnoty)
        {
            var originalValue = originalByDurationType.GetValueOrDefault(planned.TypId);
            if (originalValue != planned.Hodnota)
            {
                result[planned.TypId] = $"Původní hodnota: {originalValue} dnů | Navržená hodnota: {planned.Hodnota} dnů";
            }
        }

        foreach (var actual in payload.ActualHarmonogramHodnoty)
        {
            var originalValue = originalByDelayType.GetValueOrDefault(actual.TypId);
            if (originalValue != actual.Hodnota)
            {
                result[actual.TypId] = $"Původní hodnota: {originalValue} dnů | Navržená hodnota: {actual.Hodnota} dnů";
            }
        }

        return result;
    }

    private void ConfigureProposalDetailEditor(
        ZaznamEditViewModel model,
        ZaznamNavrhEntity proposal,
        bool canDecide,
        IReadOnlyDictionary<string, string>? changedFieldTooltips = null,
        IReadOnlyDictionary<int, string>? changedScheduleTypeTooltips = null,
        string? proposalSummaryNote = null)
    {
        var isCreateProposal = string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase);
        var isPending = string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase);

        model.PageTitle = isCreateProposal
            ? $"Detail návrhu založení záznamu #{proposal.Id}"
            : $"Detail návrhu změny termínu a harmonogramu #{proposal.Id}";
        model.ModalTitle = model.PageTitle;
        model.IsProposalDecisionDetail = true;
        model.ProposalId = proposal.Id;
        model.ProposalType = proposal.TypNavrhu;
        model.ProposalState = proposal.Stav;
        model.ProposalSummaryNote = proposalSummaryNote;
        model.AllowBasicMetadataEdit = false;
        model.AllowTermDeadlineEdit = false;
        model.CanEditRecord = false;
        model.CanEditScheduleFull = false;
        model.CanEditScheduleAddOnly = false;
        model.PrimaryActionLabel = string.Empty;
        model.FormAction = string.Empty;
        model.FormController = string.Empty;
        model.ActiveEditorTab = "basic";
        model.HarmonogramBlok = CloneScheduleBlock(
            model.HarmonogramBlok,
            permissions: ScheduleEditorPermissionSet.ForReadOnly(),
            editorChangedTypeTooltips: changedScheduleTypeTooltips);
        model.CanApproveProposal = canDecide && isPending;
        model.CanRejectProposal = canDecide && isPending;
        model.CanRejectAndEditProposal = canDecide && isPending;
        model.CanPrefillProposalForm = canDecide
            && !isPending
            && isCreateProposal;
        model.ProposalChangedFieldTooltips = changedFieldTooltips ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        model.ProposalChangedScheduleTypeTooltips = changedScheduleTypeTooltips ?? new Dictionary<int, string>();
    }

    private static HarmonogramBlockViewModel CloneScheduleBlock(
        HarmonogramBlockViewModel source,
        HarmonogramSouhrnViewModel? souhrn = null,
        IReadOnlyList<HarmonogramKrokEditViewModel>? kroky = null,
        DateTime? terminUkonceni = null,
        ScheduleEditorPermissionSet? permissions = null,
        IReadOnlyDictionary<int, string>? editorChangedTypeTooltips = null)
    {
        return new HarmonogramBlockViewModel
        {
            RecordId = source.RecordId,
            Mode = source.Mode,
            DatumZalozeni = source.DatumZalozeni,
            TerminUkonceni = terminUkonceni ?? source.TerminUkonceni,
            DelayBarvaHex = source.DelayBarvaHex,
            Souhrn = souhrn ?? source.Souhrn,
            Kroky = kroky ?? source.Kroky,
            Permissions = permissions ?? source.Permissions,
            EditorChangedTypeTooltips = editorChangedTypeTooltips ?? source.EditorChangedTypeTooltips,
            ScheduleVersion = source.ScheduleVersion,
            // Plán D Task 8/9 passthrough (M-1 fix) — lock state + edit capability zachovat.
            LockedManualKrokKeys = source.LockedManualKrokKeys,
            CanEditManualActual = source.CanEditManualActual
        };
    }

    private RecordProposalListItemViewModel BuildProposalListItem(
        ProposalListRow proposal,
        CurrentUserContextViewModel currentUser,
        bool canDecide,
        IReadOnlyDictionary<int, string> peopleById,
        IReadOnlyDictionary<int, string> subsystemById,
        IReadOnlyDictionary<int, ProposalRecordRow> recordRowsById)
    {
        var payload = DeserializePayload(proposal.PayloadJson);
        var createPayload = payload.CreateRecord;
        var schedulePayload = payload.SchedulePlan;
        recordRowsById.TryGetValue(proposal.ZaznamId ?? 0, out var recordRow);

        return new RecordProposalListItemViewModel
        {
            Id = proposal.Id,
            ProjektId = proposal.ProjektId,
            ZaznamId = proposal.ZaznamId,
            ApprovedRecordId = proposal.ApprovedRecordId,
            TypNavrhu = proposal.TypNavrhu,
            TypNavrhuLabel = GetProposalTypeLabel(proposal.TypNavrhu),
            Stav = proposal.Stav,
            StavLabel = GetProposalStateLabel(proposal.Stav),
            Subsystem = subsystemById.GetValueOrDefault(proposal.SubsystemId, "-"),
            Autor = peopleById.GetValueOrDefault(proposal.CreatedByOsobaId, $"Osoba #{proposal.CreatedByOsobaId}"),
            CreatedAt = proposal.CreatedAt,
            RozhodlUzivatel = proposal.DecidedByOsobaId.HasValue
                ? peopleById.GetValueOrDefault(proposal.DecidedByOsobaId.Value, $"Osoba #{proposal.DecidedByOsobaId.Value}")
                : null,
            DecidedAt = proposal.DecidedAt,
            Nazev = createPayload?.Nazev ?? recordRow?.Nazev,
            Cil = createPayload?.Cil ?? recordRow?.Cil,
            CisloViditelne = recordRow?.CisloViditelne,
            DatumZalozeni = createPayload?.DatumZalozeni ?? recordRow?.DatumZalozeni,
            TerminUkonceni = createPayload?.TerminUkonceni ?? schedulePayload?.TerminUkonceni ?? recordRow?.TerminUkonceni,
            PlannedStepCount = schedulePayload?.PlannedHarmonogramHodnoty.Count ?? 0,
            ActualStepCount = schedulePayload?.ActualHarmonogramHodnoty.Count ?? 0,
            CanApprove = canDecide && string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase),
            CanReject = canDecide && string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase),
            CanRejectAndTakeOver = canDecide
                && string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase)
                && string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase),
            CanRejectAndEdit = canDecide && string.Equals(proposal.Stav, RecordProposalStateCodes.Pending, StringComparison.OrdinalIgnoreCase),
            CanPrefillCreateForm = canDecide && string.Equals(proposal.TypNavrhu, RecordProposalTypeCodes.CreateRecord, StringComparison.OrdinalIgnoreCase),
            DetailUrl = null,
            ScheduleProposalEditorUrl = null,
            PrefillCreateFormUrl = null
        };
    }

    private static string GetProposalTypeLabel(string typeCode)
        => string.Equals(typeCode, RecordProposalTypeCodes.SchedulePlanChange, StringComparison.OrdinalIgnoreCase)
            ? "Návrh změny termínu a harmonogramu"
            : "Návrh založení záznamu";

    private static string GetProposalStateLabel(string stateCode)
        => stateCode.ToUpperInvariant() switch
        {
            RecordProposalStateCodes.Approved => "Schváleno",
            RecordProposalStateCodes.Rejected => "Zamítnuto",
            _ => "Čeká na rozhodnutí"
        };

    private async Task EnsureCanViewProposalAsync(ZaznamNavrhEntity proposal, CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        var access = await _authorizationPolicy.EvaluateProjectAccessAsync(proposal.ProjektId, currentUser, ct);
        if (!access.CanViewTab)
        {
            throw new InvalidOperationException("Nemáte přístup k návrhům v tomto projektu.");
        }

        if (!access.CanDecide && proposal.CreatedByOsobaId != currentUser.OsobaId)
        {
            throw new InvalidOperationException("Nemáte oprávnění zobrazit detail tohoto návrhu.");
        }
    }
}
