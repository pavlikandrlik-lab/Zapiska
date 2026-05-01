using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private async Task<List<ProjektHarmonogramUkolViewModel>> BuildProjectScheduleRowsAsync(IReadOnlyList<ZaznamCardViewModel> records, CancellationToken ct)
    {
        var taskRecords = records
            .Where(x => x.JeUkol)
            .ToList();

        if (taskRecords.Count == 0)
        {
            return [];
        }

        var taskRecordIds = taskRecords.Select(x => x.Id).ToList();
        var harmonogramRows = await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => taskRecordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);
        var harmonogramByRecord = harmonogramRows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, int>)group.ToDictionary(item => item.TypId, item => item.HodnotaInt));
        var schemaCache = new Dictionary<int, HarmonogramSchemaDefinition>();
        var ownerIds = taskRecords.Select(x => x.AktualniVlastnikId).Distinct().ToList();
        var ownerRows = await dbContext.Osoby.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni
            })
            .ToListAsync(ct);
        var ownerById = ownerRows.ToDictionary(x => x.Id);
        var schemaVersions = taskRecords
            .Select(x => x.HarmonogramSablonaVerze > 0 ? x.HarmonogramSablonaVerze : 0)
            .Distinct()
            .ToList();
        foreach (var schemaVersion in schemaVersions)
        {
            schemaCache[schemaVersion] = await harmonogramService.GetSchemaForRecordAsync(schemaVersion, ct);
        }

        return taskRecords
            .Select(record =>
            {
                var deadline = (record.AktualniTermin ?? record.DatumZalozeni).Date;
                var schemaVersion = record.HarmonogramSablonaVerze > 0 ? record.HarmonogramSablonaVerze : 0;
                var schema = schemaCache[schemaVersion];

                var harmonogramHodnoty = harmonogramByRecord.TryGetValue(record.Id, out var harmonogramValues)
                    ? harmonogramValues
                    : EmptyIntMap;
                var vypocet = harmonogramService.BuildHarmonogramVypocetPublic(record.DatumZalozeni, schema.Kroky, harmonogramHodnoty);
                var souhrn = harmonogramService.BuildHarmonogramSouhrn(vypocet, deadline);
                var owner = ownerById.GetValueOrDefault(record.AktualniVlastnikId);
                var ownerDisplay = owner is null
                    ? record.AktualniVlastnik
                    : BuildDisplayName(owner.Titul, owner.Jmeno, owner.Prijmeni, owner.Id);
                var hasVisualDuration = souhrn.CelkoveTrvaniDni > 0;

                if (!hasVisualDuration)
                {
                    return null;
                }

                var sharedSteps = vypocet
                    .Select(krok => new HarmonogramKrokEditViewModel
                    {
                        KrokIndex = krok.KrokIndex,
                        Nazev = krok.Nazev,
                        BarvaHex = krok.BarvaHex,
                        TrvaniTypId = krok.TrvaniTypId,
                        ZpozdeniTypId = krok.ZpozdeniTypId,
                        TrvaniDni = krok.TrvaniDni,
                        OdchylkaDni = krok.ZpozdeniDni,
                        BaselineDatum = krok.BaselineDatum.Date,
                        SkutecneDatum = krok.PosunuteDatum.Date
                    })
                    .ToList();

                return new ProjektHarmonogramUkolViewModel
                {
                    ZaznamId = record.Id,
                    CisloViditelne = record.CisloViditelne,
                    Nazev = record.Nazev,
                    TypUkolu = record.TypUkolu,
                    TypUkoluKod = record.TypUkoluKod,
                    Stav = record.Stav,
                    StavKod = record.StavKod,
                    SubsystemKod = record.AktualniSubsystemKod,
                    Subsystem = record.AktualniSubsystem,
                    SubsystemPoradi = record.AktualniSubsystemPoradi,
                    SubsystemHasProjectOrder = record.AktualniSubsystemHasProjectOrder,
                    Vlastnik = ownerDisplay,
                    VlastnikOsobaId = record.AktualniVlastnikId,
                    KategorieKod = record.KategorieKod,
                    Kategorie = record.KategorieNazev,
                    JeAktivni = record.IsAktivniStav,
                    JednaniVyjadreniStavyKody = record.VyjadreniJednaniStavyKody,
                    Stihame = souhrn.Stihame,
                    HarmonogramBlok = BuildScheduleBlockViewModel(
                        record.Id,
                        "project-readonly",
                        record.DatumZalozeni,
                        deadline,
                        schema.DelayBarvaHex,
                        souhrn,
                        sharedSteps)
                };
            })
            .Where(x => x is not null)
            .Cast<ProjektHarmonogramUkolViewModel>()
            .ToList();
    }
}
