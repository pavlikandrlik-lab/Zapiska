using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services;

public sealed partial class RecordService
{
    public async Task<ZaznamEditViewModel> BuildZaznamEditAsync(int id, IRecordEditorQueriesComposition composition, CancellationToken ct = default)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Záznam {id} nebyl nalezen.");

        return await composition.BuildZaznamEditForEntityAsync(record, isCreate: false, ct: ct);
    }

    public async Task<ZaznamEditViewModel> BuildZaznamCreateAsync(int projektId, int? jednaniId, IRecordEditorQueriesComposition composition, CancellationToken ct = default)
    {
        var project = await dbContext.Projekty.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == projektId, ct)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var defaultCategory = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .FirstOrDefaultAsync(ct);
        var defaultStatus = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .FirstOrDefaultAsync(ct);
        var activeProjectSubsystems = await composition.BuildActiveProjectSubsystemsAsync(projektId, ct);
        var defaultSubsystem = activeProjectSubsystems.FirstOrDefault();
        var meetings = await composition.BuildJednaniListAsync(projektId, ct);
        var meetingById = meetings.ToDictionary(x => x.Id);
        var openMeetingOptions = await composition.BuildOpenMeetingOptionsAsync(meetings, ct);
        var contextMeeting = jednaniId.HasValue ? meetingById.GetValueOrDefault(jednaniId.Value) : null;
        var selectedMeetingIdForNumber = project.PouzivatIdentJednani
            ? composition.ResolveSelectedMeetingIdForNumber(openMeetingOptions, contextMeeting?.Id)
            : null;
        var selectedMeetingForNumber = selectedMeetingIdForNumber.HasValue
            ? openMeetingOptions.FirstOrDefault(x => x.Id == selectedMeetingIdForNumber.Value)
            : null;
        var defaultStartDate = selectedMeetingForNumber?.Datum
            ?? contextMeeting?.Datum
            ?? timeProvider.GetLocalNow().Date;
        var defaultOwnerBySubsystemId = await composition.BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(projektId, ct);
        var defaultSubsystemOwnerId = defaultSubsystem is null
            ? null
            : (int?)defaultOwnerBySubsystemId.GetValueOrDefault(defaultSubsystem.SubsystemId);

        var defaultOwnerId = defaultSubsystemOwnerId
            ?? await dbContext.ObsazeniProjektu.AsNoTracking()
                .Where(x => x.ProjektId == projektId && !x.DatumOdebrani.HasValue)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.OsobaId)
                .FirstOrDefaultAsync(ct)
            ?? await dbContext.Osoby.AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync(ct);
        var nextRecordNumber = await composition.GetNextCisloZaznamuAsync(projektId, ct);
        var createVisibleNumber = project.PouzivatIdentJednani
            ? string.Empty
            : nextRecordNumber.ToString(CultureInfo.InvariantCulture);

        var draft = new ProjektovyZaznamEntity
        {
            Id = 0,
            ProjektId = projektId,
            KategorieId = defaultCategory?.Id ?? 0,
            AktualniTypUkoluId = null,
            StavUkoluId = defaultStatus?.Id,
            CisloZaznamu = nextRecordNumber,
            CisloViditelne = createVisibleNumber,
            CisloViditelneTyp = RecordDisplayNumberTypeIncrement,
            CisloViditelneA = nextRecordNumber,
            CisloViditelneB = 0,
            CisloJednaniZdrojId = null,
            Nazev = string.Empty,
            Cil = string.Empty,
            Popis = string.Empty,
            VlastnikId = defaultOwnerId,
            DatumZalozeni = defaultStartDate.Date,
            DatumUkonceni = timeProvider.GetLocalNow().Date,
            SubsystemId = defaultSubsystem?.SubsystemId ?? 0
        };

        return await composition.BuildZaznamEditForEntityAsync(
            draft,
            isCreate: true,
            forceMeetingIdForNumber: selectedMeetingIdForNumber,
            projectUsesMeetingIdentifier: project.PouzivatIdentJednani,
            openMeetingOptions: openMeetingOptions,
            ct: ct);
    }

    public async Task<DeleteRecordModalViewModel> BuildDeleteRecordModalAsync(int projektId, int zaznamId, CancellationToken ct = default)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == zaznamId)
            .Select(x => new
            {
                x.Id,
                x.ProjektId,
                x.KategorieId,
                x.StavUkoluId,
                x.Nazev,
                x.CisloViditelne,
                x.CisloZaznamu
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Záznam {zaznamId} nebyl nalezen.");

        if (record.ProjektId != projektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        var categoryName = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId)
            .Select(x => x.Nazev)
            .FirstOrDefaultAsync(ct) ?? "-";
        var stateName = record.StavUkoluId.HasValue
            ? await dbContext.CiselnikStavuUkolu.AsNoTracking()
                .Where(x => x.Id == record.StavUkoluId.Value)
                .Select(x => x.Nazev)
                .FirstOrDefaultAsync(ct) ?? "-"
            : "-";

        // Counts paralelně přes paralelní queries by byly rychlejší, ale dbContext
        // není thread-safe pro souběžné enumerace. Sekvenčně je to ~9 round-tripů na DB,
        // pro intranet acceptable. Při potřebě perf optimalizace lze přepsat na single
        // SELECT s 9 sub-counts.
        var vyjadreniCount = await dbContext.Vyjadreni.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);
        var externiVazbyCount = await dbContext.ZaznamExterniOdkazy.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);
        var spolupraceCount = await dbContext.ZaznamSpoluprace.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);
        var harmonogramCount = await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);
        var harvestCount = await dbContext.VyjadreniVazby.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);
        var navrhyTargetCount = await dbContext.ZaznamNavrhy.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);
        var navrhyOriginCount = await dbContext.ZaznamNavrhy.AsNoTracking().CountAsync(x => x.ApprovedRecordId == zaznamId, ct);
        var historieCount =
            await dbContext.ZaznamHistorieZmenTypu.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct)
            + await dbContext.ZaznamHistorieTerminu.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct)
            + await dbContext.ZaznamHistorieVlastnik.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct)
            + await dbContext.ZaznamHistorieSubsystem.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct)
            + await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct)
            + await dbContext.ZaznamHistorieStavuProjektu.AsNoTracking().CountAsync(x => x.ZaznamId == zaznamId, ct);

        return new DeleteRecordModalViewModel
        {
            Title = "Smazat záznam natrvalo",
            Command = new DeleteRecordCommand
            {
                ProjektId = projektId,
                ZaznamId = zaznamId
            },
            CisloViditelne = string.IsNullOrWhiteSpace(record.CisloViditelne)
                ? record.CisloZaznamu.ToString(CultureInfo.InvariantCulture)
                : record.CisloViditelne.Trim(),
            Nazev = record.Nazev,
            Kategorie = categoryName,
            Stav = stateName,
            VyjadreniCount = vyjadreniCount,
            ExterniVazbyCount = externiVazbyCount,
            SpolupraceCount = spolupraceCount,
            HarmonogramCount = harmonogramCount,
            HarvestVyjadreniCount = harvestCount,
            NavrhyTargetCount = navrhyTargetCount,
            NavrhyOriginCount = navrhyOriginCount,
            HistorieCount = historieCount
        };
    }
}
