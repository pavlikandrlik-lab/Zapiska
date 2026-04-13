using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

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

        var subsystemRows = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => subsystemIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Kod,
                x.Nazev
            })
            .ToListAsync(ct);
        var subsystemsById = subsystemRows.ToDictionary(x => x.Id);

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
        var categories = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking().OrderBy(x => x.Nazev).ToListAsync(ct);
        var taskStates = await dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToListAsync(ct);
        var taskTypes = await dbContext.CiselnikTypuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToListAsync(ct);
        var projectSubsystems = await BuildRecordEditorProjectSubsystemsAsync(record.ProjektId, record.SubsystemId, ct);
        var defaultOwnerBySubsystemId = await BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(record.ProjektId, ct);
        var ownerCandidates = await BuildRecordOwnerCandidatesAsync(record.ProjektId, record.VlastnikId, ct);
        var collaborationCandidates = await BuildRecordOwnerCandidatesAsync(record.ProjektId, null, ct);

        var extTypes = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().OrderBy(x => x.Kod).ToListAsync(ct);
        var vyzvyById = (await dbContext.CiselnikVyzvy.AsNoTracking()
                .OrderBy(x => x.Kod)
                .Select(x => new
                {
                    x.Id,
                    x.Kod
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.Kod);

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
                    Vyzva = x.Vyzva.HasValue ? vyzvyById.GetValueOrDefault(x.Vyzva.Value) : null,
                    DatumObjednani = x.DatumObjednani,
                    PlanDodani = x.PlanDodani,
                    DatumDodani = x.DatumDodani,
                    DatumPrevzeti = x.DatumPrevzeti
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
        var harmonogramValues = (!isCreate && isTaskCategory && allowedTypeIds.Count > 0)
            ? (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                    .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
                    .ToListAsync(ct))
                .ToDictionary(x => x.TypId, x => x.HodnotaInt)
            : new Dictionary<int, int>();
        var harmonogramKroky = harmonogramService.BuildHarmonogramVypocetPublic(record.DatumZalozeni, harmonogramTypy, harmonogramValues);
        var harmonogramSouhrn = harmonogramService.BuildHarmonogramSouhrn(harmonogramKroky, record.DatumUkonceni);
        var harmonogramBlokKroky = harmonogramKroky.Select(krok => new HarmonogramKrokEditViewModel
        {
            KrokIndex = krok.KrokIndex,
            Nazev = krok.Nazev,
            BarvaHex = krok.BarvaHex,
            TrvaniTypId = krok.TrvaniTypId,
            ZpozdeniTypId = krok.ZpozdeniTypId,
            TrvaniDni = krok.TrvaniDni,
            OdchylkaDni = krok.ZpozdeniDni,
            BaselineDatum = krok.BaselineDatum,
            SkutecneDatum = krok.PosunuteDatum
        }).ToList();

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
                editorJeUkolKategorie: isTaskCategory),
            DostupniVlastnici = ownerCandidates,
            DostupniSpolupracovnici = collaborationCandidates,
            VybraniSpolupracovniciIds = selectedCollaborationIds,
            ExterniVazby = externalLinks,
            TypyExternichOdkazu = extTypes.Select(x => x.Kod).ToList(),
            Vyzvy = vyzvyById.Values.ToList()
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
