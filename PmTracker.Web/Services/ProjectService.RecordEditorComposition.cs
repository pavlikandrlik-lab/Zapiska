using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private async Task<List<ProjectSubsystemViewModel>> BuildRecordEditorProjectSubsystemsAsync(int projectId, int currentSubsystemId, CancellationToken ct)
    {
        var mappings = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .Select(x => new
            {
                x.Id,
                x.SubsystemId,
                x.Poradi
            })
            .ToListAsync(ct);

        var subsystemIds = mappings
            .Select(x => x.SubsystemId)
            .Append(currentSubsystemId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var allSubsystems = await lookupCache.GetSubsystemsAsync(ct);
        var subsystemsById = subsystemIds
            .Where(allSubsystems.ContainsKey)
            .ToDictionary(id => id, id => allSubsystems[id]);

        return subsystemIds
            .Select(subsystemId =>
            {
                var subsystem = subsystemsById.GetValueOrDefault(subsystemId);
                var mapping = mappings.FirstOrDefault(x => x.SubsystemId == subsystemId);

                return new ProjectSubsystemViewModel
                {
                    ProjektSubsystemId = mapping?.Id ?? 0,
                    SubsystemId = subsystemId,
                    Poradi = mapping?.Poradi ?? 0,
                    Kod = subsystem?.Kod ?? "-",
                    Nazev = subsystem?.Nazev ?? "-",
                    DatumPrirazeni = DateTime.MinValue
                };
            })
            .OrderBy(x => x.Kod, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<ZaznamEditViewModel> BuildZaznamEditForEntityAsync(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber = null,
        bool? projectUsesMeetingIdentifier = null,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions = null,
        CancellationToken ct = default)
    {
        // Perf: číselníky přes scoped cache (OrderBy se dělá in-memory — tabulky mají <50 řádků, negligible).
        var categories = (await lookupCache.GetCategoriesAsync(ct)).Values.OrderBy(x => x.Nazev).ToList();
        var taskStates = (await lookupCache.GetTaskStatesAsync(ct)).Values.OrderBy(x => x.Nazev).ToList();
        var taskTypes = (await lookupCache.GetTaskTypesAsync(ct)).Values.OrderBy(x => x.Nazev).ToList();
        var projectSubsystems = await BuildRecordEditorProjectSubsystemsAsync(record.ProjektId, record.SubsystemId, ct);
        var defaultOwnerBySubsystemId = await BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(record.ProjektId, ct);
        var ownerCandidates = await BuildRecordOwnerCandidatesAsync(record.ProjektId, record.VlastnikId, ct);
        var collaborationCandidates = await BuildRecordOwnerCandidatesAsync(record.ProjektId, null, ct);

        var extTypes = (await lookupCache.GetExternalLinkTypesAsync(ct)).Values.OrderBy(x => x.Kod).ToList();
        var vyzvyById = (await lookupCache.GetVyzvyAsync(ct))
            .ToDictionary(x => x.Key, x => x.Value.Kod);

        var projectUsesMeetingNumbering = projectUsesMeetingIdentifier
            ?? await dbContext.Projekty.AsNoTracking()
                .Where(x => x.Id == record.ProjektId)
                .Select(x => (bool?)x.PouzivatIdentJednani)
                .FirstOrDefaultAsync(ct)
            ?? false;
        var meetingOptions = openMeetingOptions ?? await BuildOpenMeetingOptionsAsync(await BuildJednaniListAsync(record.ProjektId, ct), ct);
        var selectedMeetingId = forceMeetingIdForNumber
            ?? (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting ? record.CisloJednaniZdrojId : null);

        var externalLinks = isCreate
            ? new List<ExterniOdkazEditViewModel>()
            : (await dbContext.ZaznamExterniOdkazy.AsNoTracking()
                    .Where(x => x.ZaznamId == record.Id)
                    .OrderBy(x => x.Id)
                    .ToListAsync(ct))
                .Select(x => new ExterniOdkazEditViewModel
                {
                    Id = x.Id,
                    Typ = extTypes.FirstOrDefault(et => et.Id == x.TypOdkazuId)?.Kod ?? string.Empty,
                    Cislo = x.Cislo,
                    PredpokladanaCena = x.PredpokladanaCena,
                    VyzvaId = x.VyzvaId,
                    VyzvaKod = x.VyzvaId.HasValue ? vyzvyById.GetValueOrDefault(x.VyzvaId.Value) : null,
                    ZaradidDoVyzvy = x.ZaradidDoVyzvy,
                    DatumObjednani = x.DatumObjednani,
                    PlanDodani = x.PlanDodani,
                    DatumDodani = x.DatumDodani,
                    DatumPrevzeti = x.DatumPrevzeti,
                    LastHarvestedAt = x.LastHarvestedAt
                })
                .ToList();

        var selectedCollaborationIds = isCreate
            ? new List<int>()
            : await dbContext.ZaznamSpoluprace.AsNoTracking().Where(x => x.ZaznamId == record.Id).Select(x => x.OsobaId).ToListAsync(ct);

        var selectedCategory = categories.FirstOrDefault(x => x.Id == record.KategorieId);
        var isTaskCategory = IsTaskCategory(selectedCategory?.Kod, selectedCategory?.Nazev);
        var harmonogramSchema = isCreate
            ? await harmonogramService.GetActiveHarmonogramSchemaAsync(ct)
            : await harmonogramService.GetSchemaForRecordAsync(record, ct);
        var harmonogramTypy = harmonogramSchema.Kroky;
        var allowedTypeIds = harmonogramTypy
            .SelectMany(x => new[] { x.TrvaniTypId, x.ZpozdeniTypId })
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        // Plán 4 Feature C Task 6: načti plný záznam včetně Feature C metadat (SkutecnostZdroj,
        // SkutecnostRezim, PreferredExterniOdkazId) — VM je vystaví per krok pro UI toggle/badge.
        var harmonogramRows = (!isCreate && isTaskCategory && allowedTypeIds.Count > 0)
            ? await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
                .ToListAsync(ct)
            : new List<PmTracker.Web.Models.Entities.ZaznamHarmonogramHodnotaEntity>();
        var harmonogramValues = harmonogramRows.ToDictionary(x => x.TypId, x => x.HodnotaInt);
        var harmonogramRowsByTypId = harmonogramRows.ToDictionary(x => x.TypId);
        var harmonogramKroky = harmonogramService.BuildHarmonogramVypocetPublic(record.DatumZalozeni, harmonogramTypy, harmonogramValues);
        var harmonogramSouhrn = harmonogramService.BuildHarmonogramSouhrn(harmonogramKroky, record.DatumUkonceni);

        // Plán D Task 8/9 composition: pro každý krok zjistit KrokKey (stabilní GUID identifikátor),
        // zdroj skutečnosti (FromVyjadreni / Manual / None) a označit ruční kroky {2, 5, 8, 9}.
        // KrokKey pochází z CiselnikHarmonogramTypu, resolver dohledá aktivní vazbu nebo manuální
        // HS0X_DELAY zápis.
        var krokKeyByDurationTypId = allowedTypeIds.Count > 0
            ? await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => !x.JeZpozdeni && allowedTypeIds.Contains(x.Id))
                .Select(x => new { x.Id, x.KrokKey })
                .ToDictionaryAsync(x => x.Id, x => x.KrokKey, ct)
            : new Dictionary<int, Guid>();
        var scheduleActualSources = (!isCreate && isTaskCategory && record.Id > 0)
            ? await scheduleActualSourceResolver.ResolveForRecordAsync(record.Id, record.HarmonogramSablonaVerze, ct)
            : new Dictionary<Guid, RecordScheduleActualSource>();

        // Plán 4 Feature C Task 6: načti pending proposal lock PŘED build Kroky, aby canToggleRezim
        // mohl být použitý per krok (dříve bylo načteno až za Kroky — přesun je no-op pro existing code
        // dole, ale umožňuje VM obohacení Feature C metadaty inline).
        var pendingScheduleProposalLock = !isCreate
            ? await pendingScheduleProposalLockEvaluator.EvaluateAsync(record.Id, ct)
            : new PendingScheduleProposalLockState(false, null, null, false, false);

        // CanToggleRezim derivujeme ze stejné compozice jako CanEditManualActual
        // (isTaskCategory + schedule není zamčený pending návrhem) — sdílejí stejnou editability condition.
        var canToggleRezim = isTaskCategory && !pendingScheduleProposalLock.LocksSchedule;

        // Plán 4 Feature C Task 6 UI (dropdown) — jeden batch load bindings pro záznam.
        // Resolver per krok potom filtruje přes predikát matici NES/PMP/PNF × KrokPoradi.
        var bindingKandidati = (!isCreate && isTaskCategory && record.Id > 0)
            ? await harmonogramSkutecnostSync.GetKandidatiForZaznamAsync(record.Id, ct).ConfigureAwait(false)
            : Array.Empty<BindingKandidat>() as IReadOnlyList<BindingKandidat>;

        var harmonogramBlokKroky = harmonogramKroky.Select(krok =>
        {
            var krokKey = krokKeyByDurationTypId.GetValueOrDefault(krok.TrvaniTypId);
            var source = krokKey != Guid.Empty && scheduleActualSources.TryGetValue(krokKey, out var s)
                ? s
                : null;
            // Feature C metadata z HS0X_DELAY row (ZpozdeniTypId). Pokud řádek ještě neexistuje
            // (krok má plánovou hodnotu, skutečnost není zapsaná), delayRow je null → defaulty.
            harmonogramRowsByTypId.TryGetValue(krok.ZpozdeniTypId, out var delayRow);

            // Plán 4 Feature C Task 6 UI (dropdown) — resolve kandidátní bindings pro tento krok.
            // Resolver vrátí kandidáty seřazené MAX first + označí vybraný (preferred nebo MAX).
            // Pokud kroků je víc než 1 v UI, chevron ▼ se zobrazí.
            var resolved = PmTracker.Web.Services.Schedules.HarmonogramSkutecnostResolver.Resolve(
                krok.KrokIndex, bindingKandidati, delayRow?.PreferredExterniOdkazId);
            var kandidatiVm = resolved.Kandidati.Count == 0
                ? (IReadOnlyList<HarmonogramKrokKandidatViewModel>)Array.Empty<HarmonogramKrokKandidatViewModel>()
                : resolved.Kandidati.Select(k => new HarmonogramKrokKandidatViewModel
                {
                    ExterniOdkazId = k.ExterniOdkazId,
                    Cislo6 = k.Cislo6,
                    TypZaznamu = k.TypZaznamu,
                    Datum = k.Datum,
                    IsSelected = k.ExterniOdkazId == resolved.VybranyExterniOdkazId
                }).ToList();

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
                KrokKey = krokKey,
                ZdrojSkutecnosti = source?.Zdroj ?? ZdrojSkutecnosti.None,
                SourceVyjadreniId = source?.SourceVyjadreniId,
                SourceVyjadreniDatum = source?.SourceVyjadreniDatum,
                SourceExterniOdkazId = source?.SourceExterniOdkazId,
                IsManualKrok = HarmonogramManualSteps.IsManual(krok.KrokIndex),
                // Plán 4 Feature C Task 6 — UI metadata pro switch + badge
                DelayHodnotaId = delayRow?.Id,
                SkutecnostRezim = delayRow?.SkutecnostRezim ?? PmTracker.Web.Models.Entities.SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = delayRow?.SkutecnostZdroj ?? PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Neznamo,
                PreferredExterniOdkazId = delayRow?.PreferredExterniOdkazId,
                CanToggleRezim = canToggleRezim,
                Kandidati = kandidatiVm
            };
        }).ToList();

        // F-11: Soft concurrency check — načti MAX(UpdatedAt) harmonogramových hodnot jako version stamp
        var scheduleVersion = string.Empty;
        if (!isCreate && isTaskCategory && record.Id > 0)
        {
            var maxUpdatedAt = await dbContext.ZaznamHarmonogramHodnoty
                .Where(x => x.ZaznamId == record.Id)
                .MaxAsync(x => (DateTime?)x.UpdatedAt, ct);
            if (maxUpdatedAt.HasValue)
            {
                scheduleVersion = maxUpdatedAt.Value.Ticks.ToString("X16");
            }
        }

        return new ZaznamEditViewModel
        {
            Id = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            ProjektId = record.ProjektId,
            IsCreate = isCreate,
            PouzivatIdentJednani = projectUsesMeetingNumbering,
            MaDostupneJednaniProCislo = meetingOptions.Count > 0,
            MuzeDoplnitIdentifikatorJednani = !isCreate && projectUsesMeetingNumbering && record.CisloViditelneTyp != RecordDisplayNumberTypeMeeting,
            JednaniIdProCislo = selectedMeetingId,
            JednaniProCisloOptions = meetingOptions,
            Nazev = record.Nazev,
            Cil = record.Cil ?? string.Empty,
            Kategorie = selectedCategory?.Nazev ?? string.Empty,
            Popis = record.Popis ?? string.Empty,
            TypUkolu = record.AktualniTypUkoluId.HasValue ? taskTypes.FirstOrDefault(x => x.Id == record.AktualniTypUkoluId.Value)?.Nazev : null,
            Stav = record.StavUkoluId.HasValue ? taskStates.FirstOrDefault(x => x.Id == record.StavUkoluId.Value)?.Nazev ?? string.Empty : string.Empty,
            KategorieZaznamu = categories.Select(x => x.Nazev).ToList(),
            StavyUkolu = taskStates.Select(x => x.Nazev).ToList(),
            TypyUkolu = taskTypes.Select(x => x.Nazev).ToList(),
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystemy = projectSubsystems.Select(x => new SubsystemOptionViewModel
            {
                Kod = x.Kod,
                Nazev = x.Nazev,
                DefaultOwnerOsobaId = defaultOwnerBySubsystemId.GetValueOrDefault(x.SubsystemId)
            }).ToList(),
            Subsystem = projectSubsystems.FirstOrDefault(x => x.SubsystemId == record.SubsystemId)?.Nazev ?? string.Empty,
            Vlastnici = ownerCandidates.Select(x => new LookupOptionViewModel
            {
                Value = x.OsobaId.ToString(CultureInfo.InvariantCulture),
                Label = string.IsNullOrWhiteSpace(x.Email)
                    ? x.Osoba
                    : $"{x.Osoba} <{textNormalizer.NormalizeEmail(x.Email)}>"
            }).ToList(),
            VlastnikId = record.VlastnikId,
            JeUkolKategorie = isTaskCategory,
            HarmonogramBlok = BuildScheduleBlockViewModel(
                record.Id,
                "record-editor",
                record.DatumZalozeni,
                record.DatumUkonceni,
                harmonogramSchema.DelayBarvaHex,
                harmonogramSouhrn,
                harmonogramBlokKroky,
                permissions: pendingScheduleProposalLock.LocksSchedule
                    ? ScheduleEditorPermissionSet.ForActiveScheduleProposal(isTaskCategory)
                    : ScheduleEditorPermissionSet.ForFullEdit(isTaskCategory),
                scheduleVersion: scheduleVersion,
                // Plán D Task 8/9: lock manuálních kroků z pending návrhu + povolit editaci
                // jen pokud harmonogram není v read-only režimu (full-edit permissions).
                lockedManualKrokKeys: pendingScheduleProposalLock.LockedManualKrokKeys,
                canEditManualActual: isTaskCategory && !pendingScheduleProposalLock.LocksSchedule),
            DostupniVlastnici = ownerCandidates,
            DostupniSpolupracovnici = collaborationCandidates,
            VybraniSpolupracovniciIds = selectedCollaborationIds,
            ExterniVazby = externalLinks,
            TypyExternichOdkazu = extTypes.Select(x => x.Kod).ToList(),
            ModalTitle = isCreate ? "Nový projektový záznam" : $"Upravit záznam #{ResolveVisibleRecordNumber(record)}",
            PrimaryActionLabel = isCreate ? "Založit záznam" : "Uložit",
            HasPendingScheduleProposalLock = pendingScheduleProposalLock.HasPendingProposal,
            PendingScheduleProposalId = pendingScheduleProposalLock.ProposalId,
            PendingScheduleProposalMessage = pendingScheduleProposalLock.Message,
            PendingScheduleProposalLocksTermDeadline = pendingScheduleProposalLock.LocksTermDeadline,
            PendingScheduleProposalLocksSchedule = pendingScheduleProposalLock.LocksSchedule
        };
    }

    private List<JednaniOptionViewModel> BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings)
    {
        return meetings
            .Where(IsMeetingOpenForRecordNumbering)
            .OrderByDescending(x => x.CisloJednani)
            .Select(x => new JednaniOptionViewModel
            {
                Id = x.Id,
                Label = $"Jednání č. {x.CisloJednani} ({x.Datum:dd.MM.yyyy})",
                Datum = x.Datum.Date
            })
            .ToList();
    }

    private Task<List<JednaniOptionViewModel>> BuildOpenMeetingOptionsAsync(IReadOnlyList<JednaniListItemViewModel> meetings, CancellationToken ct)
        => Task.FromResult(BuildOpenMeetingOptions(meetings));

    private static int? ResolveSelectedMeetingIdForNumber(
        IReadOnlyList<JednaniOptionViewModel> openMeetingOptions,
        int? contextMeetingId)
    {
        if (contextMeetingId.HasValue && openMeetingOptions.Any(x => x.Id == contextMeetingId.Value))
        {
            return contextMeetingId.Value;
        }

        return openMeetingOptions.FirstOrDefault()?.Id;
    }

    private bool IsMeetingOpenForRecordNumbering(JednaniListItemViewModel meeting)
    {
        return !string.IsNullOrWhiteSpace(meeting.StavKod)
            && !Ci.Equals(meeting.StavKod, "CLOSED")
            && !meeting.Stav.Contains("uzav", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(meeting.UzamklOsoba);
    }
}
