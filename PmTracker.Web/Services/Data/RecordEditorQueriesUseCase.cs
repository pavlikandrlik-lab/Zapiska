using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class RecordEditorQueriesUseCase(PmTrackerDbContext dbContext) : IRecordEditorQueriesUseCase
{
    private const byte RecordDisplayNumberTypeIncrement = 0;

    public ZaznamEditViewModel BuildZaznamEdit(int id, IRecordEditorQueriesComposition composition)
    {
        var record = dbContext.ProjektoveZaznamy.AsNoTracking().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Záznam {id} nebyl nalezen.");

        return composition.BuildZaznamEditForEntity(record, isCreate: false);
    }

    public ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId, IRecordEditorQueriesComposition composition)
    {
        var project = dbContext.Projekty.AsNoTracking()
            .FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var defaultCategory = dbContext.CiselnikKategoriiZaznamu.AsNoTracking().OrderBy(x => x.Nazev).FirstOrDefault();
        var defaultStatus = dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).FirstOrDefault();
        var activeProjectSubsystems = composition.BuildActiveProjectSubsystems(projektId);
        var defaultSubsystem = activeProjectSubsystems.FirstOrDefault();
        var meetings = composition.BuildJednaniList(projektId);
        var meetingById = meetings.ToDictionary(x => x.Id);
        var openMeetingOptions = composition.BuildOpenMeetingOptions(meetings);
        var contextMeeting = jednaniId.HasValue ? meetingById.GetValueOrDefault(jednaniId.Value) : null;
        var selectedMeetingIdForNumber = project.PouzivatIdentJednani
            ? composition.ResolveSelectedMeetingIdForNumber(openMeetingOptions, contextMeeting?.Id)
            : null;
        var selectedMeetingForNumber = selectedMeetingIdForNumber.HasValue
            ? openMeetingOptions.FirstOrDefault(x => x.Id == selectedMeetingIdForNumber.Value)
            : null;
        var defaultStartDate = selectedMeetingForNumber?.Datum
            ?? contextMeeting?.Datum
            ?? DateTime.Today;
        var defaultSubsystemOwnerId = defaultSubsystem is null
            ? null
            : (int?)composition.BuildDefaultOwnerOsobaIdsByProjectSubsystem(projektId).GetValueOrDefault(defaultSubsystem.SubsystemId);

        var defaultOwnerId = defaultSubsystemOwnerId
            ?? dbContext.ObsazeniProjektu.AsNoTracking()
                .Where(x => x.ProjektId == projektId && !x.DatumOdebrani.HasValue)
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.OsobaId)
                .FirstOrDefault()
            ?? dbContext.Osoby.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).First();
        var activeSchemaVersion = composition.EnsurePersistedActiveHarmonogramSchemaVersion();
        var nextRecordNumber = composition.GetNextCisloZaznamu(projektId);
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
            DatumUkonceni = DateTime.Today,
            SubsystemId = defaultSubsystem?.SubsystemId ?? 0,
            HarmonogramSablonaVerze = activeSchemaVersion
        };

        return composition.BuildZaznamEditForEntity(draft, isCreate: true, forceMeetingIdForNumber: selectedMeetingIdForNumber, projectUsesMeetingIdentifier: project.PouzivatIdentJednani, openMeetingOptions: openMeetingOptions);
    }

    public DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId)
    {
        var record = dbContext.ProjektoveZaznamy.AsNoTracking()
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
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Záznam {zaznamId} nebyl nalezen.");

        if (record.ProjektId != projektId)
        {
            throw new InvalidOperationException("Záznam nepatří do vybraného projektu.");
        }

        var categoryName = dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId)
            .Select(x => x.Nazev)
            .FirstOrDefault() ?? "-";
        var stateName = record.StavUkoluId.HasValue
            ? dbContext.CiselnikStavuUkolu.AsNoTracking()
                .Where(x => x.Id == record.StavUkoluId.Value)
                .Select(x => x.Nazev)
                .FirstOrDefault() ?? "-"
            : "-";

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
            VyjadreniCount = dbContext.Vyjadreni.AsNoTracking().Count(x => x.ZaznamId == zaznamId),
            ExterniVazbyCount = dbContext.ZaznamExterniOdkazy.AsNoTracking().Count(x => x.ZaznamId == zaznamId),
            SpolupraceCount = dbContext.ZaznamSpoluprace.AsNoTracking().Count(x => x.ZaznamId == zaznamId),
            HarmonogramCount = dbContext.ZaznamHarmonogramHodnoty.AsNoTracking().Count(x => x.ZaznamId == zaznamId)
        };
    }
}
